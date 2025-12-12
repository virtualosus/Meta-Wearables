package com.metawearables.unity;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.util.Base64;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import com.meta.wearable.mwdat.core.*;
import com.meta.wearable.mwdat.camera.*;
import com.meta.wearable.mwdat.core.device.*;
import com.meta.wearable.mwdat.core.session.*;
import com.meta.wearable.mwdat.camera.capture.*;
import com.meta.wearable.mwdat.camera.stream.*;

import java.io.ByteArrayOutputStream;
import java.nio.ByteBuffer;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Unity bridge for Meta Wearables Device Access Toolkit
 * Handles communication between Unity and Meta smart glasses
 */
public class MetaWearablesBridge implements DeviceConnectionListener, PhotoCaptureListener, VideoStreamListener {
    
    private static final String TAG = "MetaWearablesBridge";
    
    // Unity callback object
    private String unityGameObject;
    
    // Android context
    private Activity activity;
    private Context context;
    
    // Meta Wearables SDK components
    private MWDatSession session;
    private MWDevice connectedDevice;
    private CameraManager cameraManager;
    private VideoStreamManager videoStreamManager;
    
    // Configuration
    private String applicationId;
    private boolean analyticsEnabled;
    
    // Threading
    private ExecutorService executorService;
    
    // Stream settings
    private int videoQuality = VideoStreamQuality.MEDIUM;
    private boolean isStreaming = false;
    
    /**
     * Initialize the Meta Wearables bridge
     */
    public void initialize(Activity activity, String appId, boolean enableAnalytics, String unityObject) {
        Log.d(TAG, "Initializing Meta Wearables Bridge");
        
        this.activity = activity;
        this.context = activity.getApplicationContext();
        this.applicationId = appId;
        this.analyticsEnabled = enableAnalytics;
        this.unityGameObject = unityObject;
        
        executorService = Executors.newCachedThreadPool();
        
        // Initialize Meta Wearables SDK
        initializeSDK();
    }
    
    /**
     * Initialize the Meta Wearables SDK
     */
    private void initializeSDK() {
        try {
            // Create configuration
            MWDatConfiguration config = new MWDatConfiguration.Builder()
                .setApplicationId(applicationId)
                .setAnalyticsOptOut(!analyticsEnabled)
                .build();
            
            // Initialize SDK
            MWDat.initialize(context, config);
            
            // Create session
            session = MWDat.createSession();
            session.addConnectionListener(this);
            
            Log.d(TAG, "Meta Wearables SDK initialized successfully");
            
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize SDK: " + e.getMessage());
            sendError("SDK initialization failed: " + e.getMessage());
        }
    }
    
    /**
     * Connect to Meta Wearables device
     */
    public void connect() {
        Log.d(TAG, "Attempting to connect to device");
        
        executorService.execute(() -> {
            try {
                // Get available devices
                List<MWDevice> devices = session.getAvailableDevices();
                
                if (devices.isEmpty()) {
                    sendError("No Meta Wearables devices found");
                    return;
                }
                
                // Connect to first available device
                MWDevice device = devices.get(0);
                session.connect(device);
                
            } catch (Exception e) {
                Log.e(TAG, "Connection failed: " + e.getMessage());
                sendError("Connection failed: " + e.getMessage());
            }
        });
    }
    
    /**
     * Disconnect from device
     */
    public void disconnect() {
        Log.d(TAG, "Disconnecting from device");
        
        if (connectedDevice != null) {
            try {
                if (isStreaming) {
                    stopVideoStream();
                }
                
                session.disconnect(connectedDevice);
                connectedDevice = null;
                cameraManager = null;
                videoStreamManager = null;
                
            } catch (Exception e) {
                Log.e(TAG, "Disconnect failed: " + e.getMessage());
            }
        }
    }
    
    /**
     * Capture a photo
     */
    public void capturePhoto() {
        Log.d(TAG, "Capturing photo");
        
        if (cameraManager == null) {
            sendError("Camera not available");
            return;
        }
        
        executorService.execute(() -> {
            try {
                PhotoCaptureRequest request = new PhotoCaptureRequest.Builder()
                    .setQuality(PhotoQuality.HIGH)
                    .build();
                
                cameraManager.capturePhoto(request, this);
                
            } catch (Exception e) {
                Log.e(TAG, "Photo capture failed: " + e.getMessage());
                sendError("Photo capture failed: " + e.getMessage());
            }
        });
    }
    
    /**
     * Start video streaming
     */
    public void startVideoStream() {
        Log.d(TAG, "Starting video stream");
        
        if (videoStreamManager == null) {
            sendError("Video stream not available");
            return;
        }
        
        if (isStreaming) {
            Log.w(TAG, "Video stream already active");
            return;
        }
        
        executorService.execute(() -> {
            try {
                VideoStreamRequest request = new VideoStreamRequest.Builder()
                    .setQuality(videoQuality)
                    .setFrameRate(30)
                    .build();
                
                videoStreamManager.startStream(request, this);
                isStreaming = true;
                
            } catch (Exception e) {
                Log.e(TAG, "Failed to start video stream: " + e.getMessage());
                sendError("Video stream failed: " + e.getMessage());
            }
        });
    }
    
    /**
     * Stop video streaming
     */
    public void stopVideoStream() {
        Log.d(TAG, "Stopping video stream");
        
        if (videoStreamManager != null && isStreaming) {
            try {
                videoStreamManager.stopStream();
                isStreaming = false;
            } catch (Exception e) {
                Log.e(TAG, "Failed to stop video stream: " + e.getMessage());
            }
        }
    }
    
    /**
     * Set video stream quality
     */
    public void setVideoQuality(int quality) {
        // 0 = Low (360p), 1 = Medium (540p), 2 = High (720p)
        switch (quality) {
            case 0:
                videoQuality = VideoStreamQuality.LOW;
                break;
            case 1:
                videoQuality = VideoStreamQuality.MEDIUM;
                break;
            case 2:
                videoQuality = VideoStreamQuality.HIGH;
                break;
            default:
                videoQuality = VideoStreamQuality.MEDIUM;
        }
        
        // If streaming, restart with new quality
        if (isStreaming) {
            stopVideoStream();
            startVideoStream();
        }
    }
    
    /**
     * Toggle flash/torch
     */
    public void toggleFlash(boolean enabled) {
        if (cameraManager != null) {
            try {
                cameraManager.setFlashEnabled(enabled);
            } catch (Exception e) {
                Log.e(TAG, "Failed to toggle flash: " + e.getMessage());
            }
        }
    }
    
    // ========== Device Connection Callbacks ==========
    
    @Override
    public void onDeviceConnected(MWDevice device) {
        Log.d(TAG, "Device connected: " + device.getId());
        
        connectedDevice = device;
        
        // Initialize camera manager
        try {
            cameraManager = new CameraManager(session, device);
            videoStreamManager = new VideoStreamManager(session, device);
        } catch (Exception e) {
            Log.e(TAG, "Failed to initialize camera: " + e.getMessage());
        }
        
        // Notify Unity
        activity.runOnUiThread(() -> {
            UnityPlayer.UnitySendMessage(unityGameObject, "OnAndroidDeviceConnected", device.getId());
        });
    }
    
    @Override
    public void onDeviceDisconnected(MWDevice device) {
        Log.d(TAG, "Device disconnected: " + device.getId());
        
        connectedDevice = null;
        cameraManager = null;
        videoStreamManager = null;
        isStreaming = false;
        
        // Notify Unity
        activity.runOnUiThread(() -> {
            UnityPlayer.UnitySendMessage(unityGameObject, "OnAndroidDeviceDisconnected", device.getId());
        });
    }
    
    @Override
    public void onConnectionError(MWDevice device, ConnectionError error) {
        Log.e(TAG, "Connection error: " + error.getMessage());
        sendError("Connection error: " + error.getMessage());
    }
    
    // ========== Photo Capture Callbacks ==========
    
    @Override
    public void onPhotoCaptured(PhotoData photoData) {
        Log.d(TAG, "Photo captured");
        
        executorService.execute(() -> {
            try {
                // Get photo bytes
                byte[] imageBytes = photoData.getImageData();
                
                // Convert to base64 for Unity
                String base64Image = Base64.encodeToString(imageBytes, Base64.NO_WRAP);
                
                // Send to Unity
                activity.runOnUiThread(() -> {
                    UnityPlayer.UnitySendMessage(unityGameObject, "OnAndroidPhotoReceived", base64Image);
                });
                
            } catch (Exception e) {
                Log.e(TAG, "Failed to process photo: " + e.getMessage());
                sendError("Photo processing failed: " + e.getMessage());
            }
        });
    }
    
    @Override
    public void onPhotoCaptureError(CaptureError error) {
        Log.e(TAG, "Photo capture error: " + error.getMessage());
        sendError("Photo capture error: " + error.getMessage());
    }
    
    // ========== Video Stream Callbacks ==========
    
    @Override
    public void onVideoFrameReceived(VideoFrame frame) {
        // Process frame on background thread to avoid blocking
        executorService.execute(() -> {
            try {
                // Get frame data
                ByteBuffer buffer = frame.getData();
                int width = frame.getWidth();
                int height = frame.getHeight();
                
                // Convert to byte array
                byte[] frameBytes = new byte[buffer.remaining()];
                buffer.get(frameBytes);
                
                // Convert to bitmap
                Bitmap bitmap = BitmapFactory.decodeByteArray(frameBytes, 0, frameBytes.length);
                
                // Compress to JPEG
                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                bitmap.compress(Bitmap.CompressFormat.JPEG, 80, baos);
                byte[] jpegBytes = baos.toByteArray();
                
                // Convert to base64
                String base64Frame = Base64.encodeToString(jpegBytes, Base64.NO_WRAP);
                
                // Send to Unity (throttle if needed)
                activity.runOnUiThread(() -> {
                    UnityPlayer.UnitySendMessage(unityGameObject, "OnAndroidVideoFrame", base64Frame);
                });
                
                // Clean up
                bitmap.recycle();
                baos.close();
                
            } catch (Exception e) {
                Log.e(TAG, "Failed to process video frame: " + e.getMessage());
            }
        });
    }
    
    @Override
    public void onStreamError(StreamError error) {
        Log.e(TAG, "Video stream error: " + error.getMessage());
        sendError("Stream error: " + error.getMessage());
        isStreaming = false;
    }
    
    @Override
    public void onStreamStarted() {
        Log.d(TAG, "Video stream started");
    }
    
    @Override
    public void onStreamStopped() {
        Log.d(TAG, "Video stream stopped");
        isStreaming = false;
    }
    
    // ========== Helper Methods ==========
    
    /**
     * Send error message to Unity
     */
    private void sendError(String error) {
        activity.runOnUiThread(() -> {
            UnityPlayer.UnitySendMessage(unityGameObject, "OnAndroidError", error);
        });
    }
    
    /**
     * Clean up resources
     */
    public void cleanup() {
        Log.d(TAG, "Cleaning up Meta Wearables Bridge");
        
        if (isStreaming) {
            stopVideoStream();
        }
        
        if (connectedDevice != null) {
            disconnect();
        }
        
        if (session != null) {
            session.removeConnectionListener(this);
            session.dispose();
            session = null;
        }
        
        if (executorService != null) {
            executorService.shutdown();
            executorService = null;
        }
    }
}
