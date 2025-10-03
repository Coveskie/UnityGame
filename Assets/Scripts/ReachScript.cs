using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ReachStick : MonoBehaviour
{
    public PlayerInteractor interactor;
    Collider _col;

    void Reset()
    {
        _col = GetComponent<Collider>();
        if (_col) _col.isTrigger = true;
    }

    void Awake()
    {
        _col = GetComponent<Collider>();
        if (_col && !_col.isTrigger)
        {
            Debug.LogWarning("ReachStick: Collider was not Trigger. Setting isTrigger = true.");
            _col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (interactor) interactor.OnReachTriggerEnter(other);
    }



    void OnTriggerExit(Collider other)
    {
        if (interactor) interactor.OnReachTriggerExit(other);
    }
}
