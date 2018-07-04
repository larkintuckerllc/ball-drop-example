using System;
using UnityEngine;
using UnityEngine.Experimental.XR.MagicLeap;

public class Ball : MonoBehaviour {

    // HANDS
    static MLHandKeyPose H_KEYPOSE = MLHandKeyPose.Fist;
    static float H_KEYPOSE_CONFIDENCE_THRESHOLD = 0.5f;

    // INPUT
    static float I_SPEED_HORZ = 0.3f;
    static uint I_TOUCH_CONTROL = 0;

    // WORLD PLANES
    static uint WP_MAX_PLANES = 100;

    // WORLD RAYS
    static float WR_CENTIMETER = 0.01f;
    static Vector3 WR_DIRECTION = new Vector3(0.0f, -1.0f, 0.0f);
    static float WR_SPEED_VERT = 0.3f;

    // PLANES
    public GameObject pPrefab;

    // HANDS
    bool _hFirst = true;
    bool _hLastKeypose = false;

    // INPUT
    MLInputController _iController;

    // WORLD RAYS
    float _wrBaseHeight;
    bool _wrChangingHeight = false;
    int _wrLastCentimeters;
    bool _wrPending = false;

    void Awake()
    {
        MLResult hResult = MLHands.Start();
        MLResult iResult = MLInput.Start();
        MLResult pResult = MLWorldPlanes.Start();
        MLResult wrResult = MLWorldRays.Start();
        if (
            !hResult.IsOk ||
            !iResult.IsOk ||
            !pResult.IsOk ||
            !wrResult.IsOk
        )
        {
            Debug.LogError("Error starting ML APIs, disabling script.");
            enabled = false;
            return;
        }

        // HANDS
        var enabledPoses = new MLHandKeyPose[] {
            MLHandKeyPose.Fist,
        };
        MLHands.KeyPoseManager.EnableKeyPoses(enabledPoses, true);

        // INPUT
        _iController = MLInput.GetController(MLInput.Hand.Left);

        // WORLD PLANES
        var queryParams = new MLWorldPlanesQueryParams();
        var queryFlags = MLWorldPlanesQueryFlags.Horizontal | MLWorldPlanesQueryFlags.SemanticFloor;
        queryParams.Flags = queryFlags;
        queryParams.MaxResults = WP_MAX_PLANES;
        MLWorldPlanes.GetPlanes(queryParams, HandleOnReceivedPlanes);

        // WORLD RAYS
        _wrBaseHeight = transform.position.y;
        _wrLastCentimeters = (int)Math.Truncate(_wrBaseHeight * 100);
    }

    void Update()
    {
        if (!_iController.Connected)
        {
            Debug.Log("Error controller not connected");
            return;
        }
        float deltaTime = Time.deltaTime;

        // HANDS
        var keypose = false;
        var hand = MLHands.Right;
        if (hand.KeyPose == H_KEYPOSE && hand.KeyPoseConfidence > H_KEYPOSE_CONFIDENCE_THRESHOLD)
        {
            keypose = true;
        }
        if (_hFirst)
        {
            _hFirst = false;
            _hLastKeypose = !_hLastKeypose;
        }
        if (keypose != _hLastKeypose)
        {
            _hLastKeypose = keypose;
            if (keypose) {
                _wrChangingHeight = false;
                gameObject.AddComponent<Rigidbody>();
            } else {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) {
                    Destroy(rb);
                }
                Vector3 position = transform.position;
                position.y = _wrBaseHeight;
                transform.position = position;
            }
        }

        // INPUT
        if (!_hLastKeypose)
        {
            var touch = _iController.State.TouchPosAndForce[I_TOUCH_CONTROL];
            var position = transform.position;
            position.x += I_SPEED_HORZ * touch.x * deltaTime;
            position.z += I_SPEED_HORZ * touch.y * deltaTime;
            transform.position = position;
        }

        // WORLD RAYS
        if (!_wrPending && !_hLastKeypose) {
            _wrPending = true;
            var raycastParams = new MLWorldRays.QueryParams();
            raycastParams.Position = transform.position;
            raycastParams.Direction = WR_DIRECTION;
            MLWorldRays.GetWorldRays(raycastParams, HandleOnReceiveRaycast);
        }
        if (_wrChangingHeight)
        {
            var targetY = _wrLastCentimeters / 100.0f;
            var position = transform.position;
            if (position.y <= targetY)
            {
                position.y += WR_SPEED_VERT * deltaTime;
                if (targetY - position.y <= WR_CENTIMETER)
                {
                    _wrChangingHeight = false;
                    position.y = targetY;
                }
            } else {
                position.y -= WR_SPEED_VERT * deltaTime;
                if (position.y - targetY <= WR_CENTIMETER)
                {
                    _wrChangingHeight = false;
                    position.y = targetY;
                }
            }
            transform.position = position;
        }
    }

    void OnDestroy()
    {
        MLHands.Stop();
        MLInput.Stop();
        MLWorldPlanes.Stop();
        MLWorldRays.Stop();
    }

    void HandleOnReceivedPlanes(MLResult result, MLWorldPlane[] planes)
    {
        if (!result.IsOk)
        {
            Debug.LogError("Error GetPlanes");
            return;
        }
        for (int i = 0; i < planes.Length; ++i)
        {
            var plane = planes[i];
            var newPlane = Instantiate(pPrefab);
            newPlane.transform.position = plane.Center;
            newPlane.transform.rotation = plane.Rotation;
            newPlane.transform.localScale = new Vector3(plane.Width, plane.Height, 1.0f);
        }
    }

    void HandleOnReceiveRaycast(MLWorldRays.MLWorldRaycastResultState state, Vector3 point, Vector3 normal, float confidence)
    {
        _wrPending = false;
        if (_hLastKeypose) {
            return;
        }
        if (state == MLWorldRays.MLWorldRaycastResultState.RequestFailed)
        {
            Debug.Log("Request failed");
            return;
        }
        if (state == MLWorldRays.MLWorldRaycastResultState.NoCollision)
        {
            Debug.Log("No collision");
            return;
        }
        var newCentimeters = (int)Math.Truncate((_wrBaseHeight + point.y) * 100);
        if (newCentimeters != _wrLastCentimeters) {
            _wrLastCentimeters = newCentimeters;
            _wrChangingHeight = true;
        }
    }
}