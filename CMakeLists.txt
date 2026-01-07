cmake_minimum_required(VERSION 3.24)

project(T3PP_Patch_GUI_Lite LANGUAGES C CXX)

# Auto-detect existing prebuilt native DLLs and allow skipping the native build.
set(T3PP_PREBUILT_DEBUG "${CMAKE_SOURCE_DIR}/T3ppNativeLite/T3ppNativelite_Debug_x64.dll")
set(T3PP_PREBUILT_RELEASE "${CMAKE_SOURCE_DIR}/T3ppNativeLite/T3ppNativelite_Release_x64.dll")
set(_prebuilt_found FALSE)
if(EXISTS "${T3PP_PREBUILT_DEBUG}" AND EXISTS "${T3PP_PREBUILT_RELEASE}")
    set(_prebuilt_found TRUE)
endif()
option(USE_PREBUILT_T3PP_NATIVE "Use existing T3ppNativelite_*.dll instead of building the native project" ${_prebuilt_found})

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_C_STANDARD 99)
set(CMAKE_C_STANDARD_REQUIRED ON)

add_subdirectory(T3ppNativeLite)

# Optional: build the WPF frontend via dotnet (disabled by default).
option(BUILD_PATCHGUI_WPF "Invoke dotnet build for the WPF app" OFF)
if(BUILD_PATCHGUI_WPF)
    add_custom_target(PatchGUIlite ALL
        COMMAND ${CMAKE_COMMAND} -E env DOTNET_CLI_TELEMETRY_OPTOUT=1
                dotnet build ${CMAKE_SOURCE_DIR}/PatchGUIlite/PatchGUIlite.csproj -c $<CONFIG>
        WORKING_DIRECTORY ${CMAKE_SOURCE_DIR}
        COMMENT "Building PatchGUIlite (.NET)")
endif()
