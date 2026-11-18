cmake_minimum_required(VERSION 3.20)
project(BalatroTrainer LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")

include(FetchContent)
FetchContent_Declare(
  minhook
  GIT_REPOSITORY https://github.com/TsudaKageyu/minhook.git
  GIT_TAG        v1.3.4
  SOURCE_SUBDIR  _no_cmake_here
)
FetchContent_MakeAvailable(minhook)

add_library(minhook_static STATIC
  ${minhook_SOURCE_DIR}/src/buffer.c
  ${minhook_SOURCE_DIR}/src/hook.c
  ${minhook_SOURCE_DIR}/src/trampoline.c
  ${minhook_SOURCE_DIR}/src/hde/hde32.c
  ${minhook_SOURCE_DIR}/src/hde/hde64.c
)
target_include_directories(minhook_static PUBLIC ${minhook_SOURCE_DIR}/include)

add_library(BalatroTrainer SHARED dllmain.cpp)
target_link_libraries(BalatroTrainer PRIVATE minhook_static psapi)
target_compile_definitions(BalatroTrainer PRIVATE UNICODE _UNICODE NOMINMAX _CRT_SECURE_NO_WARNINGS)
set_target_properties(BalatroTrainer PROPERTIES PREFIX "")

if(MINGW)
  target_link_options(BalatroTrainer PRIVATE -static -static-libgcc -static-libstdc++)
endif()
