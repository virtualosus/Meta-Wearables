using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;

namespace MetaWearables
{
    /// <summary>
    /// Unity manager for Meta Wearables Device Access Toolkit (Android)
    /// Handles communication with Ray-Ban Meta / Oakley Meta smart glasses
    /// </summary>
    public class MetaWearablesManager : MonoBehaviour
    {
        #region Singleton
        private static MetaWearablesManager _instance;
        public static MetaWearablesManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<MetaWearablesManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("MetaWearablesManager");
                        _instance = go.AddComponent<MetaWearablesManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Events
        public delegate void OnDeviceConnected(string deviceId);
        public delegate void OnDeviceDisconnected(string deviceId);
        public delegate void OnPhotoReceived(Texture2D photo);
        public delegate void OnVideoFrameReceived(Texture2D frame);
        public delegate void OnPermissionGranted();
        public delegate void OnPermissionDenied();
        public delegate void OnError(string errorMessage);

        public event OnDeviceConnected DeviceConnected;
        public event OnDeviceDisconnected DeviceDisconnected;
        public event OnPhotoReceived PhotoReceived;
        public event OnVideoFrameReceived VideoFrameReceived;
        public event OnPermissionGranted PermissionGranted;
        public event OnPermissionDenied PermissionDenied;
        public event OnError ErrorOccurred;
        #endregion

        #region Properties
        public bool IsInitialized { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsStreaming { get; private set; }
        public string ConnectedDeviceId { get; private set; }
        
        // Configuration
        [SerializeField] private string applicationId = "YOUR_META_APP_ID";
        [SerializeField] private bool enableAnalytics = false;
        [SerializeField] private bool autoConnect = true;
        
        // Video stream properties
        private Texture2D videoTexture;
        public Texture2D VideoTexture => videoTexture;
        
        // Photo capture
        private Texture2D lastPhoto;
        public Texture2D LastPhoto => lastPhoto;
        #endregion

        #region Android Bridge
        #if UNITY_ANDROID
        private AndroidJavaObject androidBridge;
        private AndroidJavaObject currentActivity;
        private AndroidJavaClass unityPlayer;
        #endif
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            StartCoroutine(InitializeWithPermissions());
            #else
            Debug.LogWarning("Meta Wearables DAT: Running in editor - using mock mode");
            #endif
        }

        private void OnDestroy()
        {
            if (IsConnected)
            {
                Disconnect();
            }
            CleanupAndroid();
        }
        #endregion

        #region Initialization
        private IEnumerator InitializeWithPermissions()
        {
            // Request necessary permissions
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
                yield return new WaitForSeconds(0.5f);
            }

            if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH"))
            {
                Permission.RequestUserPermission("android.permission.BLUETOOTH");
                yield return new WaitForSeconds(0.5f);
            }

            if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_ADMIN"))
            {
                Permission.RequestUserPermission("android.permission.BLUETOOTH_ADMIN");
                yield return new WaitForSeconds(0.5f);
            }

            if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            {
                Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
                yield return new WaitForSeconds(0.5f);
            }

            // Initialize Android bridge
            InitializeAndroid();
        }

        private void InitializeAndroid()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Get Unity activity
                unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                
                // Create bridge to our Android plugin
                androidBridge = new AndroidJavaObject("com.metawearables.unity.MetaWearablesBridge");
                
                // Initialize with app ID and activity
                androidBridge.Call("initialize", currentActivity, applicationId, enableAnalytics, gameObject.name);
                
                IsInitialized = true;
                Debug.Log("Meta Wearables DAT initialized successfully");
                
                if (autoConnect)
                {
                    Connect();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Meta Wearables DAT: {e.Message}");
                ErrorOccurred?.Invoke(e.Message);
            }
            #endif
        }
        #endregion

        #region Connection Management
        public void Connect()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsInitialized)
            {
                Debug.LogError("Meta Wearables DAT not initialized");
                return;
            }
            
            try
            {
                androidBridge.Call("connect");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to connect: {e.Message}");
                ErrorOccurred?.Invoke(e.Message);
            }
            #else
            Debug.Log("Mock: Connecting to Meta Wearables");
            StartCoroutine(MockConnect());
            #endif
        }

        public void Disconnect()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsInitialized) return;
            
            try
            {
                if (IsStreaming)
                {
                    StopVideoStream();
                }
                androidBridge.Call("disconnect");
                IsConnected = false;
                ConnectedDeviceId = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to disconnect: {e.Message}");
            }
            #else
            Debug.Log("Mock: Disconnecting from Meta Wearables");
            IsConnected = false;
            #endif
        }
        #endregion

        #region Photo Capture
        public void CapturePhoto()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsConnected)
            {
                Debug.LogError("Not connected to device");
                return;
            }
            
            try
            {
                androidBridge.Call("capturePhoto");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to capture photo: {e.Message}");
                ErrorOccurred?.Invoke(e.Message);
            }
            #else
            Debug.Log("Mock: Capturing photo");
            StartCoroutine(MockCapturePhoto());
            #endif
        }
        #endregion

        #region Video Streaming
        public void StartVideoStream()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsConnected)
            {
                Debug.LogError("Not connected to device");
                return;
            }
            
            try
            {
                androidBridge.Call("startVideoStream");
                IsStreaming = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start video stream: {e.Message}");
                ErrorOccurred?.Invoke(e.Message);
            }
            #else
            Debug.Log("Mock: Starting video stream");
            IsStreaming = true;
            StartCoroutine(MockVideoStream());
            #endif
        }

        public void StopVideoStream()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (!IsStreaming) return;
            
            try
            {
                androidBridge.Call("stopVideoStream");
                IsStreaming = false;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to stop video stream: {e.Message}");
            }
            #else
            Debug.Log("Mock: Stopping video stream");
            IsStreaming = false;
            #endif
        }
        #endregion

        #region Callbacks from Android
        // These methods are called from the Android plugin via Unity SendMessage
        
        public void OnAndroidDeviceConnected(string deviceId)
        {
            IsConnected = true;
            ConnectedDeviceId = deviceId;
            Debug.Log($"Connected to Meta Wearables: {deviceId}");
            DeviceConnected?.Invoke(deviceId);
        }

        public void OnAndroidDeviceDisconnected(string deviceId)
        {
            IsConnected = false;
            IsStreaming = false;
            ConnectedDeviceId = null;
            Debug.Log($"Disconnected from Meta Wearables: {deviceId}");
            DeviceDisconnected?.Invoke(deviceId);
        }

        public void OnAndroidPhotoReceived(string base64Data)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64Data);
                
                if (lastPhoto == null)
                {
                    lastPhoto = new Texture2D(2, 2);
                }
                
                lastPhoto.LoadImage(imageBytes);
                Debug.Log($"Photo received: {lastPhoto.width}x{lastPhoto.height}");
                PhotoReceived?.Invoke(lastPhoto);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to process photo: {e.Message}");
            }
        }

        public void OnAndroidVideoFrame(string base64Data)
        {
            try
            {
                byte[] frameBytes = Convert.FromBase64String(base64Data);
                
                if (videoTexture == null)
                {
                    videoTexture = new Texture2D(720, 720); // Default to square format
                }
                
                videoTexture.LoadImage(frameBytes);
                VideoFrameReceived?.Invoke(videoTexture);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to process video frame: {e.Message}");
            }
        }

        public void OnAndroidError(string error)
        {
            Debug.LogError($"Meta Wearables Error: {error}");
            ErrorOccurred?.Invoke(error);
        }

        public void OnAndroidPermissionResult(string result)
        {
            if (result == "granted")
            {
                Debug.Log("Permissions granted");
                PermissionGranted?.Invoke();
            }
            else
            {
                Debug.LogError("Permissions denied");
                PermissionDenied?.Invoke();
            }
        }
        #endregion

        #region Mock Methods for Testing
        #if UNITY_EDITOR
        private IEnumerator MockConnect()
        {
            yield return new WaitForSeconds(1f);
            IsConnected = true;
            ConnectedDeviceId = "MOCK_DEVICE_001";
            DeviceConnected?.Invoke(ConnectedDeviceId);
        }

        private IEnumerator MockCapturePhoto()
        {
            yield return new WaitForSeconds(0.5f);
            
            // Create a test texture
            if (lastPhoto == null)
            {
                lastPhoto = new Texture2D(1920, 1920);
                Color[] pixels = new Color[1920 * 1920];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
                }
                lastPhoto.SetPixels(pixels);
                lastPhoto.Apply();
            }
            
            PhotoReceived?.Invoke(lastPhoto);
        }

        private IEnumerator MockVideoStream()
        {
            if (videoTexture == null)
            {
                videoTexture = new Texture2D(720, 720);
            }
            
            while (IsStreaming)
            {
                // Generate mock frame
                Color[] pixels = new Color[720 * 720];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = new Color(UnityEngine.Random.value * 0.5f, UnityEngine.Random.value, UnityEngine.Random.value * 0.5f);
                }
                videoTexture.SetPixels(pixels);
                videoTexture.Apply();
                
                VideoFrameReceived?.Invoke(videoTexture);
                yield return new WaitForSeconds(0.033f); // ~30 FPS
            }
        }
        #endif
        #endregion

        #region Cleanup
        private void CleanupAndroid()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null)
            {
                androidBridge.Call("cleanup");
                androidBridge.Dispose();
                androidBridge = null;
            }
            #endif
        }
        #endregion

        #region Helper Methods
        public void SetVideoStreamQuality(int quality)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null)
            {
                // Quality: 0 = Low (360p), 1 = Medium (540p), 2 = High (720p)
                androidBridge.Call("setVideoQuality", quality);
            }
            #endif
        }

        public void ToggleFlash(bool enabled)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (androidBridge != null && IsConnected)
            {
                androidBridge.Call("toggleFlash", enabled);
            }
            #endif
        }
        #endregion
    }
}
