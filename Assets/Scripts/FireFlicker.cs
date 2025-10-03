using UnityEngine;

public class TorchLightFlicker : MonoBehaviour
{
    private Light torchLight;
    public float minIntensity = 2f;
    public float maxIntensity = 7f;
    public float flickerSpeed = 0.2f;

    void Start()
    {
        torchLight = GetComponent<Light>();
    }

    void Update()
    {
        torchLight.intensity = Mathf.Lerp(torchLight.intensity,
            Random.Range(minIntensity, maxIntensity), flickerSpeed);
    }
}
