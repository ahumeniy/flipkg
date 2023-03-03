## Flipper Build container

**build-fap.sh Usage**

```
./build-fap.sh [options] repo_url fap_name owner/id tag_name

Options:
  -h                    shows usage
  -b branch             branch (or tag) of the app repo to build
  -B firmware_branch    branch (or tag) of the Flipper zero firmware to build for.
                        If there's already a firmware version installed, this argument
                        is ignored.
  -d source_dir         Directory within the app repo where the App code resides.
  -r                    clone submodules from app repo
  -p                    preserve artifacts (don't cleanup)
  -P patch_in_b64_gz    Apply a patch encoded in base64 and gzipped
  -l                    local build (don't upload the results). Involves -p
                        When doing a local build, owner/id and tag_name are ignored.
```

It is recommended to use the container image. The current container image is `ahumeniy/flipkg-build-server-v2` of which there's a `latest` tag which is a bare version without an specific firmware version and there are tags for an specific version of the firmware, usually a release firmware version like 0.78.1 is `official-078.1`. These versions come with the specified firmware version already cloned and prepared to build the app quickly and it's the recommended way to build apps unless you are building for another branch such as `dev`.

## Example 1: Build a simple fap for the latest release
(0.78.1 as of 2023-03-03)

Building the **Doom** game from https://github.com/ahumeniy/doom-flipper-zero

- pull the corresponding container image.
  
  `docker pull ahumeniy/flipkg-build-server-v2:official-078.1`

- Invoke the build process:

  `docker run -it --rm -v output:/App/out ahumeniy/flipkg-build-server-v2:official-078.1 -l https://github.com/ahumeniy/doom-flipper-zero`

  Where:

  - `--it`: Run on interactive mode and display its output on the current console window.
  - `--rm`: Delete the instance once the process ends
  - `-v output:/App/out`: Open or create a docker volume to store the generated file.
  - `ahumeniy/flipkg-build-server-v2:official-078.1`: The container image name
  - `-l`: produce a local output instead of uploading to Azure Stora [^1]
    
  - `https://github.com/ahumeniy/doom-flipper-zero`: The app repo URL

[^1]: Azure storage is used as part of the original project to store the generated files. You probably don't need to remove this option unless you're creating a clone of the flipkg service.

The generated file will be stored at the specified volume in a folder with the app category, in this case it's `Games`. You can access it from the mountpoint of the volume:

**Important** If you use Docker Desktop, as the environment is running inside a virtual machine itself, you have to use Docker Desktop to list and get the files from the output volume. This command will give you a mount point *inside* the virtual machine which you can't access with command line.

```
$ docker volume inspect output
[
    {
        "CreatedAt": "2023-03-03T01:45:36Z",
        "Driver": "local",
        "Labels": null,
        "Mountpoint": "/var/lib/docker/volumes/output/_data",
        "Name": "output",
        "Options": null,
        "Scope": "local"
    }
]
```

## Example 2: Build a fap from an application which resides in a directory other than the root of its repository

**Note**: The docker image pull step is omitted intentionally.

```
docker run -it --rm -v output:/App/out ahumeniy/flipkg-build-server-v2:official-078.1 -l -b unlshd-033 -d /applications/plugins/game_2048 https://github.com/DarkFlippers/unleashed-firmware.git
```

This command will build the game **2048** from the `unlshd-033` version of the [Unleashed](https://github.com/DarkFlippers/unleashed-firmware) firmware (The latest release as of 2023-03-03) which is located at the `/applications/plugins/game_2048` directory

## Example 3: Build a fap from an application which needs a patch to run on the official firmware

**Note**: Information on how to generate such patch is pending but involves using `git format-patch`, `gzip` the resulting file and encoding it in `base64`.

**Patch file:** [Here](patches/0001-patched-dice.patch)
```
docker run -it --rm -v output:/App/out ahumeniy/flipkg-build-server-v2:official-078.1 -l -b RM0228-1236-0.78.2-5eb5e99 -d /applications/plugins/dice -P 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 https://github.com/RogueMaster/flipperzero-firmware-wPlugins.git
```

This command will build the **Dice** App from the `RM0228-1236-0.78.2-5eb5e99` release of [Roguemaster](https://github.com/RogueMaster/flipperzero-firmware-wPlugins) which is located at `applications/plugins/dice` and apply the **patch** which is encoded in `base64`. This patch is necessary to successfuly build the app for the **official** firmware because it has certain features which will otherwise not run on the stock firmware and the changes are small enough or we don't want to maintain a complete fork of the source code.

## Example 4: build a simple app for the latest dev firmware

```
docker pull ahumeniy/flipkg-build-server-v2:latest
docker run -it --rm -v output-dev:/App/out ahumeniy/flipkg-build-server-v2:latest -l https://github.com/ahumeniy/doom-flipper-zero
```

**Note:** The build process will take longer since it needs to download and build the firmware.

You can speed-up the process if you want to build multiple apps for this specific version of the firmware using the following commands:

```
docker run -it --rm -v firmware-dev-base:/App/flipper --entrypoint /bin/bash ahumeniy/flipkg-build-server-v2:latest -c 'git clone https://github.com/flipperdevices/flipperzero-firmware.git flipper && cd flipper && ./fbt -c'
```

Then you can build the next app using:

```
docker run -it --rm -v firmware-dev-base:/App/flipper-base -v output:/App/out ahumeniy/flipkg-build-server-v2:latest -l -b unlshd-033 -d /applications/plugins/game_2048 https://github.com/DarkFlippers/unleashed-firmware.git
```

And it will be a little bit faster this time.

If you then want to update the `firmware-dev-base` volume to the latest version you need to:

```
docker run -it --rm -v firmware-dev-base:/App/flipper --entrypoint /bin/bash ahumeniy/flipkg-build-server-v2:latest -c 'cd flipper && git pull && ./fbt -c'
```