cmake_minimum_required(VERSION 3.0.0)

project(notification_icon VERSION 1.1.1)

add_library(notification_icon SHARED "src/main.c")
if(UNIX AND NOT APPLE)
    find_package(PkgConfig REQUIRED)
    pkg_check_modules(APPINDICATOR REQUIRED appindicator3-0.1)
    target_include_directories(notification_icon PRIVATE ${APPINDICATOR_INCLUDE_DIRS})
    target_link_libraries(notification_icon ${APPINDICATOR_LIBRARIES})
endif()
set_target_properties(notification_icon PROPERTIES VERSION ${PROJECT_VERSION})