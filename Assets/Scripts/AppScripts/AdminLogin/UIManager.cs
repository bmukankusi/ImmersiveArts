using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Lazy singleton (no Awake required by callers)
    private static UIManager _instance;
    // Mark whether this instance was auto-created by the Instance getter
    private bool _autoCreated = false;

    public static UIManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<UIManager>();
                if (_instance == null)
                {
                    // Create a persistent UIManager when none exists so services can call UIManager.Instance safely.
                    var go = new GameObject(nameof(UIManager));
                    _instance = go.AddComponent<UIManager>();
                    _instance._autoCreated = true;
                    DontDestroyOnLoad(go);
                }
                else
                {
                    // Ensure the found instance persists across scenes
                    DontDestroyOnLoad(_instance.gameObject);
                }
            }
            return _instance;
        }
    }

    [SerializeField]
    private GameObject loginPanel;

    [SerializeField]
    private GameObject registrationPanel;

    [SerializeField]
    private GameObject dashboardPanel;

    [SerializeField]
    private TMP_Text messageText;

    // Optional: allow manual registration without Awake
    public void CreateInstance()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            _autoCreated = false;
        }
    }

    public static void Register(UIManager manager)
    {
        if (manager == null) return;
        _instance = manager;
        DontDestroyOnLoad(manager.gameObject);
        manager._autoCreated = false;
    }

    // Use Start instead of Awake to register in-scene instances.
    // This avoids using Awake while still resolving duplicates and preferring bona-fide inspector instances
    // over any auto-created fallback.
    private void Start()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
            _autoCreated = false;
        }
        else if (_instance != this)
        {
            // If the existing instance was auto-created earlier, prefer the inspector instance:
            if (_instance._autoCreated)
            {
                // destroy the auto-created instance and adopt this inspector instance
                Destroy(_instance.gameObject);
                _instance = this;
                DontDestroyOnLoad(this.gameObject);
                _autoCreated = false;
            }
            else
            {
                // Otherwise, this is a duplicate inspector instance; destroy it.
                Destroy(this.gameObject);
            }
        }
    }

    public void OpenLoginPanel()
    {
        if (loginPanel == null || registrationPanel == null)
        {
            ShowMessage("UIManager: Panels not assigned in inspector.");
            return;
        }

        loginPanel.SetActive(true);
        registrationPanel.SetActive(false);
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
    }

    public void OpenRegistrationPanel()
    {
        if (loginPanel == null || registrationPanel == null)
        {
            ShowMessage("UIManager: Panels not assigned in inspector.");
            return;
        }

        registrationPanel.SetActive(true);
        loginPanel.SetActive(false);
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
    }

    public void OpenDashboardPanel()
    {
        if (dashboardPanel == null)
        {
            ShowMessage("UIManager: Dashboard panel not assigned in inspector.");
            return;
        }

        dashboardPanel.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(false);
        if (registrationPanel != null) registrationPanel.SetActive(false);
    }

    public void ShowMessage(string message, float duration = 3f)
    {
        if (messageText == null)
        {
            Debug.Log(message);
            return;
        }

        StopAllCoroutines();
        StartCoroutine(ShowMessageCoroutine(message, duration));
    }

    private IEnumerator ShowMessageCoroutine(string message, float duration)
    {
        messageText.gameObject.SetActive(true);
        messageText.text = message;
        yield return new WaitForSeconds(duration);
        messageText.text = "";
        messageText.gameObject.SetActive(false);
    }
}