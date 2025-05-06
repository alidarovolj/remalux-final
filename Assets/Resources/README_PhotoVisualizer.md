# Photo Visualizer Mode

## Overview
The Photo Visualizer mode is an alternative to AR mode for devices that don't support AR functionality. It allows users to load photos from their gallery and apply the wall painting effect to static images.

## Usage
1. Toggle between AR and Photo modes using the button in the top-right corner
2. In Photo mode, tap "Pick Image" to select an image from your gallery
3. The app will automatically detect wall areas in the image
4. Use the color buttons and opacity slider to adjust the wall paint color
5. Take screenshots and share using the camera button

## Test Images
For testing in the Unity Editor, the Photo Visualizer mode will use a placeholder image found in:
- `Assets/Resources/TestWallImage.jpg`

If you want to test with your own images, you can:
1. Add jpg/png images to the Resources folder
2. Rename them to "TestWallImage" to have them automatically loaded
3. Or modify the `LoadTestImage()` method in `PhotoVisualizerMode.cs` to use a different image path

## Technical Details
- In a real device deployment, the Photo Visualizer uses the native image picker
- Wall segmentation is applied to the loaded image similarly to AR mode
- The main difference is the input source (static image vs. camera feed)
- This mode works even on devices without gyroscope, accelerometer, or AR support

## Troubleshooting
- If the image appears distorted, check that the aspect ratio of the loaded image matches the display area
- For best results, use well-lit photos with clear wall areas
- If wall detection is inconsistent, try using images with high contrast between the wall and surroundings 