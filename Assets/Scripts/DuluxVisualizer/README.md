# AR Wall Painting System (Dulux Visualizer)

This system implements AR wall painting similar to Dulux Visualizer. It uses ARFoundation to detect walls and applies virtual paint using segmentation.

## Components Overview

1. **ARWallPaintingCreator**: Main component that sets up the complete AR scene
2. **ARWallPaintingSceneCreator**: Static utility class to easily create the AR wall painting scene
3. **WallPaintBlit**: Applies the paint effect via post-processing
4. **WallSegmentation**: Handles the segmentation of walls using machine learning

## Getting Started

### Option 1: Using the Editor Tool

1. Open the Unity editor
2. Navigate to AR > Wall Painting > Open Scene Creator
3. Configure your scene settings
4. Click "Create AR Scene"

### Option 2: Using Script

Call the static method to create a scene programmatically:

```csharp
ARWallPaintingSceneCreator.CreateARWallPaintingScene();
```

## Scene Structure

The AR wall painting scene includes:

- **AR Session**: Manages the AR tracking
- **XR Origin**: Contains the AR camera and trackables
- **Wall Segmentation**: Processes camera frames to detect walls
- **UI Controls**: Color pickers and opacity slider

## How It Works

1. The AR camera captures live video frames
2. Frames are processed by WallSegmentation using a neural network model
3. The segmentation produces a mask highlighting walls
4. WallPaintBlit applies the chosen color to the masked areas
5. The UI allows users to select colors and adjust opacity

## Requirements

- Unity 2020.3 or newer
- AR Foundation 4.1.7+
- ARKit (iOS) or ARCore (Android) packages
- Barracuda package for neural network inference

## Customization

- **Colors**: Modify the color buttons in the UI
- **Segmentation Model**: Replace the segmentation model in Assets/Resources/Models
- **Shader**: Customize the WallPaint shader for different visual effects

## Troubleshooting

If the wall detection isn't working properly:

1. Check that ARFoundation is properly configured
2. Make sure the segmentation model is loaded correctly
3. Try using the Demo mode for testing without the neural network model
4. Check console for specific error messages

## File List

- `/Assets/Scripts/DuluxVisualizer/ARWallPaintingCreator.cs`: Main scene creation component
- `/Assets/Scripts/DuluxVisualizer/ARWallPaintingSceneCreator.cs`: Static utility class for scene creation
- `/Assets/Scripts/DuluxVisualizer/WallPaintBlit.cs`: Post-processing for paint effect
- `/Assets/Scripts/WallSegmentation.cs`: Wall segmentation using ML
- `/Assets/Shaders/WallPaint.shader`: Shader for painting walls 