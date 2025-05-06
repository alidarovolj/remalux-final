# Dulux Visualizer AR Wall Painting Project

This Unity project implements an AR wall painting experience similar to the Dulux Visualizer app, allowing users to visualize walls painted in different colors in real-time using AR Foundation.

## Features

- Real-time wall detection and segmentation using ML model
- Dynamic wall painting with adjustable colors and opacity
- Photo mode for devices without AR support
- Screenshot capture and sharing
- Editor tools for quick scene setup and testing

## Requirements

- Unity 2021.3 LTS or newer
- AR Foundation 4.2+
- ARKit XR Plugin (iOS)
- ARCore XR Plugin (Android)
- Barracuda 2.0+ (for neural network inference)

## Getting Started

1. Open the project in Unity
2. Go to `Tools > AR Wall Painting > 1. Create AR Scene Template` to create a ready-to-use AR scene
3. (Optional) Create a test AR plane using `Tools > AR Wall Painting > 2. Create AR Plane Prefab`
4. Build and run on a supported AR device

## Project Structure

### Core Components

- **ARWallPaintingCreator.cs**: Main class for setting up the AR scene
- **WallSegmentation.cs**: Handles wall detection using neural network model
- **WallPaintBlit.cs**: Applies the paint effect to detected walls
- **CaptureAndShare.cs**: Handles screenshot capture and sharing
- **PhotoVisualizerMode.cs**: Provides non-AR alternative using static photos

### Editor Tools

- **CreateARSceneCommand.cs**: Creates a template AR scene
- **CreateARPlanePrefab.cs**: Creates a test AR plane prefab
- **ARSceneTools.cs**: Editor menu for scene creation
- **ARWallPrefabCreator.cs**: Editor tool for creating test AR walls

### Resources

- **wall_segmentation_model.onnx**: Neural network model for wall segmentation

## Usage

### AR Mode
1. Launch the app on a compatible AR device
2. Point the camera at walls
3. Use the color buttons to choose a paint color
4. Adjust the opacity slider to control paint intensity
5. Capture screenshots using the camera button

### Photo Mode
1. Toggle to Photo mode using the button in the top-right corner
2. Select an image from your gallery
3. Choose colors and adjust opacity as in AR mode

## Notes

- The wall segmentation model works best in well-lit environments
- For optimal results, ensure the entire wall is visible in the camera view
- Photo mode provides an alternative for devices without AR capabilities

## Troubleshooting

- If wall detection is inconsistent, try improving lighting conditions
- On some devices, you may need to move the camera slowly to allow accurate tracking
- Check that ARCore (Android) or ARKit (iOS) is supported on your device

## Credits

- Wall segmentation model based on BiseNet architecture
- AR Foundation integration for Unity's XR system
- UI design inspired by Dulux Visualizer app 