#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <psapi.h>

#include <algorithm>
#include <atomic>
#include <cstdio>
#include <cstdlib>
#include <deque>
#include <mutex>
#include <string>
#include <unordered_set>

#include "MinHook.h"

#ifndef PIPE_REJECT_REMOTE_CLIENTS
#define PIPE_REJECT_REMOTE_CLIENTS 0x00000008
#endif

namespace {

struct lua_State;
using lua_CFunction = int (*)(lua_State*);

constexpr int LUA_GLOBALSINDEX = -10002;
constexpr int LUA_TTABLE = 5;
constexpr int LUA_TFUNCTION = 6;

struct LuaApi {
    int         (*gettop)(lua_State*);
    void        (*settop)(lua_State*, int);
    int         (*checkstack)(lua_State*, int);
    int         (*type)(lua_State*, int);
    void        (*getfield)(lua_State*, int, const char*);
    void        (*setfield)(lua_State*, int, const char*);
    void        (*pushcclosure)(lua_State*, lua_CFunction, int);
    void        (*pushstring)(lua_State*, const char*);
    void        (*pushnumber)(lua_State*, double);
    void        (*pushnil)(lua_State*);
    const char* (*tolstring)(lua_State*, int, size_t*);
    int         (*pcall)(lua_State*, int, int, int);
    int         (*loadstring)(lua_State*, const char*);  // luaL_loadstring
};
LuaApi g_lua{};

template <class T>
bool Resolve(HMODULE mod, const char* name, T& out) {
    out = reinterpret_cast<T>(reinterpret_cast<void*>(GetProcAddress(mod, name)));
    return out != nullptr;
}

enum State : int { ST_WAIT_LUA = 0, ST_WAIT_GAME = 1, ST_READY = 2, ST_FAILED = 3 };

std::atomic<int>  g_state{ST_WAIT_LUA};
std::atomic<bool> g_bootstrapped{false};
std::atomic<int>  g_bootAttempts{0};
std::atomic<bool> g_busy{false};

std::mutex        g_msgMutex;
std::string       g_lastMsg;
std::atomic<unsigned> g_msgSeq{0};

struct Cmd {
    std::string name;
    double      arg = 0;
};
std::mutex        g_queueMutex;
std::deque<Cmd>   g_queue;

const std::unordered_set<std::string> kCommands = {
    "money_add",     "money_set",      "hands_add",      "discards_add",
    "handsize_add",  "jokerslots_add", "consumableslots_add",
    "chips_add",     "chips_to_blind",
};

void Log(const std::string& raw) {
    std::string msg = raw.substr(0, 240);
    for (char& c : msg)
        if (c == '\n' || c == '\r' || c == '|') c = ' ';
    {
        std::lock_guard<std::mutex> lk(g_msgMutex);
        g_lastMsg = msg;
        ++g_msgSeq;
    }
    char tmp[MAX_PATH];
    DWORD n = GetTempPathA(MAX_PATH, tmp);
    if (n == 0 || n > MAX_PATH - 32) return;
    std::string path = std::string(tmp) + "BalatroTrainer.log";
    if (FILE* f = std::fopen(path.c_str(), "a")) {
        std::fprintf(f, "[%lu] %s\n", static_cast<unsigned long>(GetTickCount()), msg.c_str());
        std::fclose(f);
    }
}

const char* kBootstrapLua = R"LUA(
if love.__bt_installed then return end
love.__bt_installed = true

local H = {}

local function in_run()
  return G ~= nil and G.STAGES ~= nil and G.STAGE == G.STAGES.RUN
     and G.GAME ~= nil and G.GAME.current_round ~= nil
end

H.money_add            = function(n) ease_dollars(n, true) end
H.money_set            = function(n) ease_dollars(n - G.GAME.dollars, true) end
H.hands_add            = function(n) ease_hands_played(n) end
H.discards_add         = function(n) ease_discard(n) end
H.handsize_add         = function(n) G.hand:change_size(n) end
H.jokerslots_add       = function(n) G.jokers.config.card_limit = G.jokers.config.card_limit + n end
H.consumableslots_add  = function(n) G.consumeables.config.card_limit = G.consumeables.config.card_limit + n end
H.chips_add            = function(n) G.GAME.chips = G.GAME.chips + n end
H.chips_to_blind       = function()
  local b = G.GAME.blind
  local target = b and b.chips
  if not target or target <= 0 then
    -- Fallback: derive the target the same way the game does when the blind object is not filled in.
    if b and b.mult and b.mult > 0 and get_blind_amount and G.GAME.round_resets and G.GAME.starting_params then
      target = b.mult * get_blind_amount(G.GAME.round_resets.ante) * G.GAME.starting_params.ante_scaling
    end
  end
  if not target or target <= 0 then
    error('no blind is active - pick a blind first (state ' .. tostring(G.STATE) ..
          ', blind chips ' .. tostring(b and b.chips) .. ')', 0)
  end
  G.GAME.chips = math.max(G.GAME.chips, target)
  return 'score set to the target - play any hand to clear the blind'
end

local orig_update = love.update
love.update = function(...)
  for _ = 1, 16 do
    local name, arg = __bt_poll()
    if not name then break end
    local fn = H[name]
    if not fn then
      __bt_log('unknown command: ' .. tostring(name))
    elseif not in_run() then
      __bt_log('start or continue a run first (' .. name .. ' ignored)')
    else
      local ok, err = pcall(fn, arg)
      if ok then
        __bt_log('ok: ' .. name .. ' ' .. tostring(arg) .. (type(err) == 'string' and (' - ' .. err) or ''))
      else
        __bt_log('error in ' .. name .. ': ' .. tostring(err))
      end
    end
  end
  return orig_update(...)
end
)LUA";

int Lua_Poll(lua_State* L) {
    Cmd c;
    {
        std::lock_guard<std::mutex> lk(g_queueMutex);
        if (g_queue.empty()) {
            g_lua.pushnil(L);
            return 1;
        }
        c = std::move(g_queue.front());
        g_queue.pop_front();
    }
    g_lua.pushstring(L, c.name.c_str());
    g_lua.pushnumber(L, c.arg);
    return 2;
}

int Lua_Log(lua_State* L) {
    if (const char* s = g_lua.tolstring(L, 1, nullptr)) Log(s);
    return 0;
}

void TryBootstrap(lua_State* L) {
    const int top = g_lua.gettop(L);
    if (!g_lua.checkstack(L, 16)) return;

    g_lua.getfield(L, LUA_GLOBALSINDEX, "G");
    const bool hasG = g_lua.type(L, -1) == LUA_TTABLE;
    g_lua.settop(L, top);
    if (!hasG) return;

    bool hasUpdate = false;
    g_lua.getfield(L, LUA_GLOBALSINDEX, "love");
    if (g_lua.type(L, -1) == LUA_TTABLE) {
        g_lua.getfield(L, -1, "update");
        hasUpdate = g_lua.type(L, -1) == LUA_TFUNCTION;
    }
    g_lua.settop(L, top);
    if (!hasUpdate) return;

    g_lua.pushcclosure(L, &Lua_Poll, 0);
    g_lua.setfield(L, LUA_GLOBALSINDEX, "__bt_poll");
    g_lua.pushcclosure(L, &Lua_Log, 0);
    g_lua.setfield(L, LUA_GLOBALSINDEX, "__bt_log");

    int rc = g_lua.loadstring(L, kBootstrapLua);
    if (rc == 0) rc = g_lua.pcall(L, 0, 0, 0);
    if (rc != 0) {
        const char* err = g_lua.tolstring(L, -1, nullptr);
        Log(std::string("bootstrap failed: ") + (err ? err : "?"));
        g_lua.settop(L, top);
        if (++g_bootAttempts >= 3) g_state = ST_FAILED;
        return;
    }
    g_lua.settop(L, top);
    g_bootstrapped = true;
    g_state = ST_READY;
    Log("ready");
}

using PushNumberFn = void (*)(lua_State*, double);
PushNumberFn g_origPushNumber = nullptr;

void Detour_PushNumber(lua_State* L, double n) {
    if (!g_bootstrapped.load(std::memory_order_relaxed) && g_bootAttempts.load() < 3) {
        if (!g_busy.exchange(true)) {
            TryBootstrap(L);
            g_busy = false;
        }
    }
    g_origPushNumber(L, n);
}

const wchar_t* kPipeName = L"\\\\.\\pipe\\BalatroTrainer";

std::string HandleLine(const std::string& line) {
    const size_t sp = line.find(' ');
    const std::string name = line.substr(0, sp);
    const std::string rest = sp == std::string::npos ? "" : line.substr(sp + 1);

    if (name == "PING") return "PONG";
    if (name == "STATUS") {
        std::lock_guard<std::mutex> lk(g_msgMutex);
        return std::to_string(g_state.load()) + "|" + std::to_string(g_msgSeq.load()) + "|" + g_lastMsg;
    }
    if (kCommands.find(name) == kCommands.end()) return "ERR unknown command";

    double arg = 0;
    if (!rest.empty()) {
        char* end = nullptr;
        const long long v = std::strtoll(rest.c_str(), &end, 10);
        if (end == rest.c_str() || *end != '\0') return "ERR bad argument";
        if (v < -1000000000LL || v > 1000000000LL) return "ERR argument out of range";
        arg = static_cast<double>(v);
    }
    if (g_state.load() != ST_READY) return "ERR not ready";

    std::lock_guard<std::mutex> lk(g_queueMutex);
    if (g_queue.size() >= 64) return "ERR queue full";
    g_queue.push_back(Cmd{name, arg});
    return "OK";
}

DWORD WINAPI PipeThread(LPVOID) {
    for (;;) {
        HANDLE pipe = CreateNamedPipeW(
            kPipeName, PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            1, 4096, 4096, 0, nullptr);
        if (pipe == INVALID_HANDLE_VALUE) {
            Sleep(1000);
            continue;
        }
        const bool connected = ConnectNamedPipe(pipe, nullptr) || GetLastError() == ERROR_PIPE_CONNECTED;
        if (connected) {
            std::string acc;
            char buf[512];
            DWORD got = 0;
            while (ReadFile(pipe, buf, sizeof(buf), &got, nullptr) && got > 0) {
                acc.append(buf, got);
                size_t pos;
                while ((pos = acc.find('\n')) != std::string::npos) {
                    std::string line = acc.substr(0, pos);
                    acc.erase(0, pos + 1);
                    if (!line.empty() && line.back() == '\r') line.pop_back();
                    const std::string reply = HandleLine(line) + "\n";
                    DWORD written = 0;
                    WriteFile(pipe, reply.data(), static_cast<DWORD>(reply.size()), &written, nullptr);
                }
                if (acc.size() > 1024) acc.clear();
            }
        }
        DisconnectNamedPipe(pipe);
        CloseHandle(pipe);
    }
}

HMODULE FindLuaModule() {
    if (HMODULE m = GetModuleHandleW(L"lua51.dll")) return m;
    HMODULE mods[1024];
    DWORD needed = 0;
    if (!EnumProcessModules(GetCurrentProcess(), mods, sizeof(mods), &needed)) return nullptr;
    const size_t n = std::min<size_t>(needed / sizeof(HMODULE), 1024);
    for (size_t i = 0; i < n; ++i) {
        if (GetProcAddress(mods[i], "luaL_loadstring") && GetProcAddress(mods[i], "lua_pushnumber"))
            return mods[i];
    }
    return nullptr;
}

DWORD WINAPI InitThread(LPVOID) {
    if (HANDLE t = CreateThread(nullptr, 0, PipeThread, nullptr, 0, nullptr)) CloseHandle(t);
    Log("backend loaded");

    HMODULE lua = nullptr;
    for (int i = 0; i < 600 && !(lua = FindLuaModule()); ++i) Sleep(100);
    if (!lua) {
        Log("could not find the game's Lua engine (lua51.dll)");
        g_state = ST_FAILED;
        return 0;
    }

    const bool ok =
        Resolve(lua, "lua_gettop", g_lua.gettop) && Resolve(lua, "lua_settop", g_lua.settop) &&
        Resolve(lua, "lua_checkstack", g_lua.checkstack) && Resolve(lua, "lua_type", g_lua.type) &&
        Resolve(lua, "lua_getfield", g_lua.getfield) && Resolve(lua, "lua_setfield", g_lua.setfield) &&
        Resolve(lua, "lua_pushcclosure", g_lua.pushcclosure) &&
        Resolve(lua, "lua_pushstring", g_lua.pushstring) &&
        Resolve(lua, "lua_pushnumber", g_lua.pushnumber) && Resolve(lua, "lua_pushnil", g_lua.pushnil) &&
        Resolve(lua, "lua_tolstring", g_lua.tolstring) && Resolve(lua, "lua_pcall", g_lua.pcall) &&
        Resolve(lua, "luaL_loadstring", g_lua.loadstring);
    if (!ok) {
        Log("Lua API exports missing");
        g_state = ST_FAILED;
        return 0;
    }

    void* target = reinterpret_cast<void*>(g_lua.pushnumber);
    g_state = ST_WAIT_GAME;
    if (MH_Initialize() != MH_OK ||
        MH_CreateHook(target, reinterpret_cast<LPVOID>(&Detour_PushNumber),
                      reinterpret_cast<LPVOID*>(&g_origPushNumber)) != MH_OK ||
        MH_EnableHook(target) != MH_OK) {
        Log("hook installation failed");
        g_state = ST_FAILED;
        return 0;
    }

    while (!g_bootstrapped.load() && g_bootAttempts.load() < 3) Sleep(100);
    Sleep(250);
    MH_DisableHook(target);
    return 0;
}

}  // namespace

extern "C" BOOL WINAPI DllMain(HMODULE module, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(module);
        if (HANDLE t = CreateThread(nullptr, 0, InitThread, nullptr, 0, nullptr)) CloseHandle(t);
    }
    return TRUE;
}
