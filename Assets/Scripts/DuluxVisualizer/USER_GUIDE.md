# AR Wall Painting User Guide

## Quick Start

1. Open Unity and load the Dulux Visualizer project.
2. From the menu bar, select **Tools → AR Wall Painting → 🆕 Create All-in-One AR Scene**
3. A new scene will be created and saved at `Assets/Scenes/ARWallPainting.unity`
4. Press Play to test in Editor or build to an AR-compatible device

## Creating Different Scene Types

### For a Complete AR Scene (Recommended)
- Select **Tools → AR Wall Painting → 🆕 Create All-in-One AR Scene**
- This creates a fully-configured AR scene that works with or without AR Foundation

### For AR Foundation-specific Scene
- Select **Tools → AR Wall Painting → Create AR Wall Painting Scene**
- Best for latest AR Foundation versions with XR Core Utils

### For Maximum Compatibility
- Select **Tools → AR Wall Painting → Create Scene With Fixed Segmentation**
- Best for projects where AR Foundation compatibility is a concern

## Using the AR Wall Painting Scene

### In Editor
1. Press Play to enter Play Mode
2. If AR Foundation is installed:
   - You'll see the camera feed with wall detection enabled
   - Detected walls will highlight with a color overlay
3. If AR Foundation is not installed:
   - Demo walls will appear in front of the camera
   - The segmentation mask will apply to these demo walls

### On Device
1. Build the scene to an AR-capable device
2. Move the device around to detect walls
3. Walls will be automatically detected and highlighted
4. Use the color picker UI to change the paint color
5. The paint will be applied only to the detected walls

## Adjusting the WallSegmentation Component

The main AR Camera has a `WallSegmentation` component with these settings:

- **Model Input**: Set the input size for the ML model (default: 128×128×3)
- **Output Texture Size**: Size of the segmentation mask (default: Screen resolution)
- **Current Mode**: Set to AR for AR Foundation, or Demo for test walls
- **Debug View**: Enable/disable the debug visualization in top-right corner

## Adjusting the WallPaintBlit Component

The `WallPaintBlit` component controls the paint application:

- **Mask Texture**: Reference to WallSegmentation's output (auto-assigned)
- **Paint Color**: Color to apply to walls (default: Red)
- **Opacity**: Transparency of the paint (0.0-1.0, default: 0.7)

## Troubleshooting

### Missing AR Components
- If you see "Missing AR Packages" dialog when creating a scene:
  - Install AR Foundation package via Package Manager
  - Reinstall XR Plugin Management if needed
  - Try the "Create Scene With Fixed Segmentation" option

### Incorrect Wall Detection
- Try adjusting the lighting in your environment
- Make sure walls have enough texture or contrast
- Adjust the input dimensions in WallSegmentation component

### Tensor Dimension Errors
- If you see "tensor dimension mismatch" errors:
  - Check that input dimensions match your ML model (128×128×3)
  - Ensure the model is properly loaded
  - Try the "Create Scene With Fixed Segmentation" option

### Performance Issues
- Lower the output texture resolution
- Disable debug visualization
- Remove unnecessary AR components like ARPlaneManager if not needed

## For Developers

### Extending the System
- `WallSegmentation.cs` handles the ML model inference
- `WallPaintBlit.cs` handles the paint overlay
- `ARSceneBuilder.cs` and `CreateSceneWithFixedSegmentation.cs` handle scene creation

To add a custom paint effect:
1. Modify the `WallPaintBlit.cs` shader or create a new one
2. Update the reference to the mask texture
3. Add your custom rendering logic

## Demos and Examples

The system includes:
- AR scene with real wall detection
- Demo scene with test walls
- Debug visualization UI

For more help, access the documentation:
**Tools → AR Wall Painting → ⚙️ Settings and Documentation...** 