package com.metawearables.unity;

import android.content.Context;
import android.util.Log;

// Unity Player
import com.unity3d.player.UnityPlayer;

// NEW SDK 0.2.1 IMPORTS
import com.meta.wearable.mwdat.core.Wearables;
import com.meta.wearable.mwdat.core.types.DeviceIdentifier;
import com.meta.wearable.mwdat.core.types.RegistrationState;
import com.meta.wearable.mwdat.camera.StreamSession;
import com.meta.wearable.mwdat.camera.types.StreamConfiguration;
import com.meta.wearable.mwdat.camera.types.StreamSessionState;
import com.meta.wearable.mwdat.camera.types.VideoQuality;
import com.meta.wearable.mwdat.core.selectors.SpecificDeviceSelector;

// Java util
import java.util.Set;
import java.util.List;
import java.util.ArrayList;

// Kotlin Interop (Required because SDK 0.2.1 is Kotlin-first)
import kotlinx.coroutines.CoroutineScope;
import kotlinx.coroutines.Dispatchers;
import kotlinx.coroutines.flow.FlowCollector;
import kotlinx.coroutines.GlobalScope;

public class MetaWearablesBridge {

    private static final String TAG = "MetaWearablesBridge";
    private Context context;
    
    // State holding
    private DeviceIdentifier activeDevice;
    private StreamSession currentStreamSession;

    // Singleton instance
    private static MetaWearablesBridge instance;

    public static MetaWearablesBridge getInstance() {
        if (instance == null) {
            instance = new MetaWearablesBridge();
        }
        return instance;
    }

    public void initialize(Context context) {
        this.context = context;
        Log.d(TAG, "Initializing Meta Wearables SDK 0.2.1...");

        // 1. Initialize the Wearables SDK
        Wearables.INSTANCE.initialize(context);

        // 2. Start observing device connection states (Kotlin Flow wrapper)
        observeDevices();
        observeRegistrationState();
    }

    // --- Device Management ---

    public void startDiscovery() {
        Log.d(TAG, "Starting Registration Flow...");
        // In 0.2.1, "Discovery" is handled by the Meta AI app registration flow
        Wearables.INSTANCE.startRegistration(context);
    }

    // Helper to observe Kotlin StateFlow for Devices from Java
    private void observeDevices() {
        // This is a simplified way to collect Flow in Java. 
        // In a real production app, you might want a proper CoroutineScope manager.
        kotlinx.coroutines.BuildersKt.launch(
            GlobalScope.INSTANCE,
            Dispatchers.getMain(),
            null,
            (scope, continuation) -> {
                Wearables.INSTANCE.getDevices().collect(new FlowCollector<Set<DeviceIdentifier>>() {
                    @Override
                    public Object emit(Set<DeviceIdentifier> devices, kotlin.coroutines.Continuation<? super kotlin.Unit> continuation) {
                        Log.d(TAG, "Devices update received: " + devices.size());
                        if (!devices.isEmpty()) {
                            // Automatically select the first device for this example
                            activeDevice = devices.iterator().next();
                            sendMessageToUnity("OnDeviceConnected", activeDevice.toString());
                        } else {
                            activeDevice = null;
                            sendMessageToUnity("OnDeviceDisconnected", "");
                        }
                        return kotlin.Unit.INSTANCE;
                    }
                }, continuation);
                return kotlin.Unit.INSTANCE;
            }
        );
    }
    
    private void observeRegistrationState() {
         kotlinx.coroutines.BuildersKt.launch(
            GlobalScope.INSTANCE,
            Dispatchers.getMain(),
            null,
            (scope, continuation) -> {
                Wearables.INSTANCE.getRegistrationState().collect(new FlowCollector<RegistrationState>() {
                    @Override
                    public Object emit(RegistrationState state, kotlin.coroutines.Continuation<? super kotlin.Unit> continuation) {
                        Log.d(TAG, "Registration State: " + state.toString());
                        // Notify Unity of state changes (REGISTERED, NOT_REGISTERED, etc.)
                        sendMessageToUnity("OnRegistrationStateChanged", state.toString());
                        return kotlin.Unit.INSTANCE;
                    }
                }, continuation);
                return kotlin.Unit.INSTANCE;
            }
        );
    }

    // --- Camera & Streaming ---

    public void startCameraPreview() {
        if (activeDevice == null) {
            Log.e(TAG, "No active device found to start camera.");
            return;
        }

        Log.d(TAG, "Starting Camera Stream...");

        // Define stream config
        StreamConfiguration config = new StreamConfiguration(
            VideoQuality.MEDIUM, // Set quality
            true // Audio enabled
        );

        // Select the specific device
        SpecificDeviceSelector selector = new SpecificDeviceSelector(activeDevice);

        // Start session
        try {
            currentStreamSession = Wearables.INSTANCE.startStreamSession(context, selector, config);
            observeStream(currentStreamSession);
        } catch (Exception e) {
            Log.e(TAG, "Failed to start stream: " + e.getMessage());
        }
    }

    public void stopCameraPreview() {
        if (currentStreamSession != null) {
            currentStreamSession.close(); // Close session to stop stream
            currentStreamSession = null;
            Log.d(TAG, "Camera Stream Stopped");
        }
    }

    private void observeStream(StreamSession session) {
         kotlinx.coroutines.BuildersKt.launch(
            GlobalScope.INSTANCE,
            Dispatchers.getIO(), // Stream data should be on IO thread
            null,
            (scope, continuation) -> {
                // Collect video frames
                session.getVideoStream().collect(new FlowCollector<Object>() {
                    @Override
                    public Object emit(Object videoFrame, kotlin.coroutines.Continuation<? super kotlin.Unit> continuation) {
                        // NOTE: 'videoFrame' type depends on SDK details (often a texture or buffer).
                        // You will need to handle the frame data here and pass it to Unity.
                        // For now, we just log it.
                        Log.v(TAG, "Video frame received");
                        return kotlin.Unit.INSTANCE;
                    }
                }, continuation);
                return kotlin.Unit.INSTANCE;
            }
        );
    }

    // --- Photo Capture ---
    
    public void capturePhoto() {
         if (activeDevice == null) {
            Log.e(TAG, "No active device.");
            return;
        }
        
        // Note: In SDK 0.2.1, Photo capture often requires an active session or specific call.
        // Check documentation for "Wearables.takePhoto" or similar on the device session.
        Log.d(TAG, "Capture Photo requested (Implementation depends on specific 0.2.1 Capability API)");
    }

    // --- Unity Communication Helper ---

    private void sendMessageToUnity(String method, String message) {
        // "MetaWearablesManager" should be the name of your GameObject in Unity
        UnityPlayer.UnitySendMessage("MetaWearablesManager", method, message);
    }
}