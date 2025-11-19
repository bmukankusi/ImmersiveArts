using UnityEngine;
using TMPro;

public class RegisterHandler : MonoBehaviour
{
    public TMP_InputField nameRegisterField;
    public TMP_InputField emailRegisterField;
    public TMP_InputField passwordRegisterField;
    public TMP_InputField passwordRegisterVerifyField;

    void Start()
    {
        if (FirebaseAuthService.Instance == null)
        {
            Debug.Log("FirebaseAuthService not present in scene. Create a GameObject with FirebaseAuthService attached.");
        }
        else
        {
            FirebaseAuthService.Instance.Initialize();
        }
    }

    public void OnRegisterButton()
    {
        if (nameRegisterField == null || emailRegisterField == null || passwordRegisterField == null || passwordRegisterVerifyField == null)
        {
            Debug.LogError("Register input fields not set.");
            return;
        }

        if (FirebaseAuthService.Instance == null)
        {
            Debug.LogError("FirebaseAuthService instance missing.");
            return;
        }

        StartCoroutine(FirebaseAuthService.Instance.Register(
            nameRegisterField.text,
            emailRegisterField.text,
            passwordRegisterField.text,
            passwordRegisterVerifyField.text));
    }
}
