using UnityEngine;
using UnityEngine.UI;

public sealed class GS2NicknamePage : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private Button proceed;
    [SerializeField] private Text message;

    internal InputField Input => input;
    internal Button Proceed => proceed;
    internal Text Message => message;

    internal void SetReferences(InputField field, Button button, Text status)
    {
        input = field;
        proceed = button;
        message = status;
    }
}
