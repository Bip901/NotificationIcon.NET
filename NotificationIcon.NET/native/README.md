# Tray

Cross-platform native implementation of a tray notification icon.

* File [tray.h](src/tray.h) was forked from [Serge Zaitsev's implementation](https://github.com/zserge/tray) to fix bugs and add threading support on Windows
* Changed build system to CMake for cross-platform compilation (try opening with Visual Studio Code)

## Build Requirements

### Linux

#### Ubuntu
```shell
sudo apt install cmake gcc libappindicator3-dev
```

#### Fedora
```shell
sudo dnf install cmake gcc-c++ libappindicator-gtk3-devel.x86_64
```

### Windows
* Visual Studio
