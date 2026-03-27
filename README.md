# CGVI-VENV

UCL CGVI Venv coursework.

## Introduction

`Whack a Mole or Be a Mole` is a two-player game created in Unity 6 (`6000.3.5f2`). One player takes the role of the mole and the other becomes the hammer. The mole moves between five holes, choosing when to appear and avoid being hit while earning points, while the hammer player tracks the mole and tries to land hits to score. The player with the higher score after one minute wins.

Players begin in the `EnterGame` lobby scene, where they create or join a room. Once two players are in the same room, they are randomly assigned either mole or hammer. Each player can switch roles up to three times before pressing ready. After both players are ready, they enter the arena scene.

The arena starts with a short tutorial stage where players can read the controls and get familiar with them. When both players trigger ready, a countdown begins and the match starts. At the end of the game, a pop-up shows the winner and final score, with options to restart, switch roles and restart, or quit back to the lobby scene.

## Interaction

### Hammer player

The hammer player uses the controller to aim and swing at the mole. The hammer can be moved across the box to target different holes, and its handle can be extended or shortened with the joystick to reach farther positions. During the tutorial stage, the hammer player can also calibrate their position before the game starts.

### Mole player

The mole player moves between the five holes by teleporting to the anchor points below them. To appear above the box, the player can either rise using a squat-like motion or use the controller-based pull-up interaction, depending on which feels more comfortable.

## Pre-built APK

A ready-to-run Android build is available for download via Google Drive: [whack-a-mole.apk](https://drive.google.com/file/d/1yVKZuZoH9vfnqFE7UHioMcZfJ-KTpVAm/view?usp=sharing). Sideload it onto a Meta Quest headset to play without building from source.

## Compiling and running the project

This project is built and run in Unity.

1. Open the project root in Unity Hub.
2. Use Unity `6000.3.5f2`.
3. Wait for Unity to finish importing assets and compiling scripts.
4. Confirm there are no compiler errors in the Console.
5. Open `File > Build Profiles` or `Build Settings` and verify these scenes are enabled:
	* `Assets/Scenes/EnterRoomScene.unity`
	* `Assets/Scenes/MotionTest.unity`
6. Open `Assets/Scenes/EnterRoomScene.unity`.
7. Press Play in the Unity Editor.

## Scene flow

Use the `EnterGame` scene flow to enter the game. In this project, that entry flow is implemented by `Assets/Scenes/EnterRoomScene.unity`.

In the entry scene, both players must:
* join the same room
* confirm their role
* mark themselves as ready

Once both players have confirmed their role and are ready, the project redirects to the `MotionTest` scene (arena scene) for actual game play.

## Third-party assets

The following free assets from the Unity Asset Store are used in this project:

- [Stylized Dungeon Props Pack](https://assetstore.unity.com/packages/3d/environments/dungeons/stylized-dungeon-props-pack-310137) — 3D environment props used for scene dressing.
- [Rope Generator](https://assetstore.unity.com/packages/tools/physics/rope-generator-316945) — Physics-based rope tool used for rope simulation.
