using UnityEngine;
using UnityEngine.UI;
using MetaWearables;
using System.Collections;
using TMPro;

/// <summary>
/// Demo UI controller for Meta Wearables integration
/// Shows how to connect, capture photos, and stream video from Ray-Ban Meta glasses
/// </summary>
public class MetaWearablesDemo : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button connectButton;
    [SerializeField] private Button disconnectButton;
    [SerializeField] private Button capturePhotoButton;
    [SerializeField] private Button startStreamButton;
    [SerializeField] private Button stopStreamButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI deviceIdText;
    
    [Header("Display")]
    [SerializeField] private RawImage videoDisplay;
    [SerializeField] private RawImage photoDisplay;
    [SerializeField] private GameObject photoPreviewPanel;
    
    [Header("Settings")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Toggle flashToggle;
    
    [Header("AI Integration")]
    [SerializeField] private Button analyzePhotoButton;
    [SerializeField] private TextMeshProUGUI aiResponseText;
    [SerializeField] private GameObject aiPanel;
    
    private MetaWearablesManager wearablesManager;
    private Texture2D currentPhoto;
    private bool isProcessingAI = false;
    
    private void Start()
    {
        // Get or create the MetaWearablesManager
        wearablesManager = MetaWearablesManager.Instance;
        
        // Set up UI listeners
        SetupUI();
        
        // Subscribe to events
        SubscribeToEvents();
        
        // Update initial UI state
        UpdateUIState();
    }
    
    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }
    
    private void SetupUI()
    {
        // Button listeners
        connectButton.onClick.AddListener(OnConnectClicked);
        disconnectButton.onClick.AddListener(OnDisconnectClicked);
        capturePhotoButton.onClick.AddListener(OnCapturePhotoClicked);
        startStreamButton.onClick.AddListener(OnStartStreamClicked);
        stopStreamButton.onClick.AddListener(OnStopStreamClicked);
        analyzePhotoButton.onClick.AddListener(OnAnalyzePhotoClicked);
        
        // Settings listeners
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        flashToggle.onValueChanged.AddListener(OnFlashToggled);
        
        // Set up quality options
        qualityDropdown.options.Clear();
        qualityDropdown.options.Add(new TMP_Dropdown.OptionData("Low (360p)"));
        qualityDropdown.options.Add(new TMP_Dropdown.OptionData("Medium (540p)"));
        qualityDropdown.options.Add(new TMP_Dropdown.OptionData("High (720p)"));
        qualityDropdown.value = 1; // Default to medium
        
        // Hide photo preview initially
        photoPreviewPanel.SetActive(false);
        aiPanel.SetActive(false);
    }
    
    private void SubscribeToEvents()
    {
        wearablesManager.DeviceConnected += OnDeviceConnected;
        wearablesManager.DeviceDisconnected += OnDeviceDisconnected;
        wearablesManager.PhotoReceived += OnPhotoReceived;
        wearablesManager.VideoFrameReceived += OnVideoFrameReceived;
        wearablesManager.ErrorOccurred += OnError;
    }
    
    private void UnsubscribeFromEvents()
    {
        if (wearablesManager != null)
        {
            wearablesManager.DeviceConnected -= OnDeviceConnected;
            wearablesManager.DeviceDisconnected -= OnDeviceDisconnected;
            wearablesManager.PhotoReceived -= OnPhotoReceived;
            wearablesManager.VideoFrameReceived -= OnVideoFrameReceived;
            wearablesManager.ErrorOccurred -= OnError;
        }
    }
    
    // UI Event Handlers
    
    private void OnConnectClicked()
    {
        statusText.text = "Connecting...";
        wearablesManager.Connect();
    }
    
    private void OnDisconnectClicked()
    {
        wearablesManager.Disconnect();
    }
    
    private void OnCapturePhotoClicked()
    {
        statusText.text = "Capturing photo...";
        wearablesManager.CapturePhoto();
    }
    
    private void OnStartStreamClicked()
    {
        statusText.text = "Starting video stream...";
        wearablesManager.StartVideoStream();
        videoDisplay.gameObject.SetActive(true);
    }
    
    private void OnStopStreamClicked()
    {
        wearablesManager.StopVideoStream();
        videoDisplay.gameObject.SetActive(false);
    }
    
    private void OnQualityChanged(int value)
    {
        wearablesManager.SetVideoStreamQuality(value);
        
        string[] qualityNames = { "Low", "Medium", "High" };
        statusText.text = $"Video quality set to {qualityNames[value]}";
    }
    
    private void OnFlashToggled(bool enabled)
    {
        wearablesManager.ToggleFlash(enabled);
        statusText.text = $"Flash {(enabled ? "enabled" : "disabled")}";
    }
    
    // Meta Wearables Event Handlers
    
    private void OnDeviceConnected(string deviceId)
    {
        statusText.text = "Connected to Meta Wearables!";
        deviceIdText.text = $"Device: {deviceId}";
        UpdateUIState();
    }
    
    private void OnDeviceDisconnected(string deviceId)
    {
        statusText.text = "Disconnected";
        deviceIdText.text = "No device connected";
        UpdateUIState();
    }
    
    private void OnPhotoReceived(Texture2D photo)
    {
        currentPhoto = photo;
        photoDisplay.texture = photo;
        photoPreviewPanel.SetActive(true);
        statusText.text = $"Photo received: {photo.width}x{photo.height}";
        
        // Enable AI analysis button
        analyzePhotoButton.interactable = true;
        aiPanel.SetActive(true);
        
        // Auto-hide preview after 5 seconds
        StartCoroutine(HidePhotoPreview());
    }
    
    private void OnVideoFrameReceived(Texture2D frame)
    {
        videoDisplay.texture = frame;
        
        // Update FPS counter (optional)
        // You could add FPS tracking here
    }
    
    private void OnError(string error)
    {
        statusText.text = $"Error: {error}";
        Debug.LogError($"Meta Wearables Error: {error}");
    }
    
    // AI Integration
    
    private void OnAnalyzePhotoClicked()
    {
        if (currentPhoto != null && !isProcessingAI)
        {
            StartCoroutine(AnalyzePhotoWithAI());
        }
    }
    
    private IEnumerator AnalyzePhotoWithAI()
    {
        isProcessingAI = true;
        analyzePhotoButton.interactable = false;
        aiResponseText.text = "Analyzing image...";
        
        // Convert texture to base64 for AI processing
        byte[] imageBytes = currentPhoto.EncodeToPNG();
        string base64Image = System.Convert.ToBase64String(imageBytes);
        
        // Here you would integrate with Claude's API or your AI model
        // For demo purposes, we'll simulate an AI response
        yield return new WaitForSeconds(2f);
        
        // Simulated AI response (replace with actual Claude API call)
        aiResponseText.text = "AI Analysis:\n" +
            "- Scene type: Indoor/Outdoor detected\n" +
            "- Objects identified: Multiple items visible\n" +
            "- Lighting: Good conditions for AR overlay\n" +
            "- Suggested action: Ready for AI-mediated content generation";
        
        isProcessingAI = false;
        analyzePhotoButton.interactable = true;
    }
    
    private IEnumerator HidePhotoPreview()
    {
        yield return new WaitForSeconds(5f);
        photoPreviewPanel.SetActive(false);
    }
    
    // UI State Management
    
    private void UpdateUIState()
    {
        bool isConnected = wearablesManager.IsConnected;
        bool isStreaming = wearablesManager.IsStreaming;
        
        // Update button states
        connectButton.interactable = !isConnected;
        disconnectButton.interactable = isConnected;
        capturePhotoButton.interactable = isConnected && !isStreaming;
        startStreamButton.interactable = isConnected && !isStreaming;
        stopStreamButton.interactable = isConnected && isStreaming;
        
        // Update settings
        qualityDropdown.interactable = isConnected && !isStreaming;
        flashToggle.interactable = isConnected;
        
        // Update displays
        if (!isConnected)
        {
            videoDisplay.gameObject.SetActive(false);
            photoPreviewPanel.SetActive(false);
            aiPanel.SetActive(false);
        }
    }
    
    // Public methods for external integration
    
    public Texture2D GetLastPhoto()
    {
        return currentPhoto;
    }
    
    public Texture2D GetVideoFrame()
    {
        return wearablesManager.VideoTexture;
    }
    
    public bool IsConnected()
    {
        return wearablesManager.IsConnected;
    }
    
    public bool IsStreaming()
    {
        return wearablesManager.IsStreaming;
    }
}
