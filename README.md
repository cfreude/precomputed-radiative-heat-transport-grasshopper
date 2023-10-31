Tamashii Renderer - Thermal Simulation
==================================

Code for the paper "[Precomputed Radiative Heat Transport for Efficient Thermal Simulation](https://doi.org/10.1111/cgf.14957)"
by [Christian Freude](https://www.cg.tuwien.ac.at/staff/ChristianFreude), [David Hahn](https://www.cg.tuwien.ac.at/staff/DavidHahn), [Florian Rist](https://gcd.tuwien.ac.at/?p=502), [Lukas Lipp](https://www.cg.tuwien.ac.at/staff/LukasLipp) and [Michael Wimmer](https://www.cg.tuwien.ac.at/staff/MichaelWimmer)

Based on the Vulkan rendering framework by [Lukas Lipp](https://www.cg.tuwien.ac.at/staff/LukasLipp).

Shield: [![CC BY 4.0][cc-by-shield]][cc-by]

This work is licensed under a
[Creative Commons Attribution 4.0 International License][cc-by].

[![CC BY 4.0][cc-by-image]][cc-by]

[cc-by]: http://creativecommons.org/licenses/by/4.0/
[cc-by-image]: https://i.creativecommons.org/l/by/4.0/88x31.png
[cc-by-shield]: https://img.shields.io/badge/License-CC%20BY%204.0-lightgrey.svg

==================================

### Coding Conventions
[Please read carefully](CODING_CONVENTIONS.md)

General Notes
=============

### Requirements:
* [CMake](https://cmake.org/ "CMake") >= 3.14.0
* [Vulkan SDK](https://vulkan.lunarg.com/sdk/home "Vulkan SDK") >= 1.2.162.0 

### Dependencies:

* submodules in `"root/external/"`
  - [spdlog](https://github.com/gabime/spdlog "spdlog")
  - [nlohmann-json](https://github.com/nlohmann/json "nlohmann-json")
  - [tinygltf](https://github.com/syoyo/tinygltf "tinygltf")
  - [tinyies](https://github.com/fknfilewalker/tinyies "tinyies")
  - [tinyldt](https://github.com/fknfilewalker/tinyldt "tinyldt")
  - [tinyexr](https://github.com/syoyo/tinyexr "tinyexr")
  - [tinyply](https://github.com/ddiakopoulos/tinyply "tinyply")
  - [tinyobjloader](https://github.com/tinyobjloader/tinyobjloader "tinyobjloader")
  - [imgui](https://github.com/ocornut/imgui "imgui")
  - [ImGuizmo](https://github.com/CedricGuillemet/ImGuizmo "ImGuizmo")
  - [imoguizmo](https://github.com/fknfilewalker/imoguizmo "imoguizmo")
  - [stb](https://github.com/nothings/stb "stb")
  - [glm](https://github.com/g-truc/glm "glm")
  - [glslang](https://github.com/KhronosGroup/glslang "glslang")
<!-- * [vcpkg](https://github.com/microsoft/vcpkg "Vcpkg")
  - [spdlog](https://github.com/gabime/spdlog "spdlog") -->

### Notes:
Clone git with submodules
```bash
git clone --recursive
```
If build script is not used, use CMake from `root/` like
```sh
# Adjust `-G <generator-name>` accordingly or remove it
cmake -H. -B_project -G "Visual Studio 16 2019" -A "x64"
```

<!-- ### vcpkg:
Clone [vcpkg](https://github.com/microsoft/vcpkg "Vcpkg") to some directory of your choice and run `vcpkg/bootstrap-vcpkg.bat/sh`. Then install the packages listed below. Now set the environment variable `VCPKG_ROOT` to your vcpkg installation or add the path to the cmake command in `make.bat` located in `root`.
##### Install the following packages
###### windows
```sh
vcpkg install spdlog:x64-windows
```
###### linux
```sh
./vcpkg install spdlog:x64-linux
```
##### Link vcpkg with cmake:
```sh
# set before calling cmake or set it permanently in the user specific environment variables
SET VCPKG_ROOT=/path/to/vcpkg
```
or
```sh
cmake -H. -B_project -G "Visual Studio 16 2019" -A "x64" -DCMAKE_TOOLCHAIN_FILE=/path/to/vcpkg/scripts/buildsystems/vcpkg.cmake
```
Note: Adjust `-G <generator-name>` accordingly or remove it -->

Compiling On Win
==================

Use the provided `make.bat` file to generate a Visual Studio project in `root/_project/`. Use `make.bat clean` to remove all project files. If changes to cmake are not visible, delete `root/_project/CMakeCache.txt` and run `make.bat` again.
```sh
make # generate project files in 'root/_project/'
make install # install release build in 'root/_install/'
make install <install_dir> # install release build in <install_dir>
make clean # delete 'root/_project/' and 'root/_install/'
```

Compiling On Linux
==================

Use the provided `makefile` file to generate a project in `root/_project/` or open the CMakeLists.txt file in an IDE like Qt Creator or CLion. Use `make clean` to remove all project files. If changes to cmake are not visible, delete `root/_project/CMakeCache.txt` and run `make.bat` again.

Maybe you have to configure some variables for the Vulkan SDK beforehand.
```
export VULKAN_SDK=/your_path_to_vulkan_sdk/VulkanSDK/1.0.37.0/x86_64
export PATH=$PATH:$VULKAN_SDK/bin
export LD_LIBRARY_PATH=$VULKAN_SDK/
export VK_LAYER_PATH=$VULKAN_SDK/etc/explicit_layer.d
```

Command Line Args
==================
```julia
# load a given scene when the programm starts
-load_scene "assets/scenes/teapot_animated/scene.gltf"
# set the default camera
-default_camera "Camera"
# set default implementation
-default_implementation "Ray Tracing"
# set window size
-window_size 1280,720
```
