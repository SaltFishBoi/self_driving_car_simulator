using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using UnityEngine;
// added system so I can use the class Math
using System;

public class CarAgent : Agent
{
    public float speed = 10f;
    public float torque = 10f;

    public int score = 0;
    public bool resetOnCollision = true;

    private Transform _track;

    public override void Initialize()
    {
        GetTrackIncrement();
    }

    private void MoveCar(float horizontal, float vertical, float dt)
    {
        float distance = speed * vertical;
        transform.Translate(distance * dt * Vector3.forward);

        float rotation = horizontal * torque * 90f;
        transform.Rotate(0f, rotation * dt, 0f);
    }

    public override void OnActionReceived(float[] vectorAction)
    {
        float horizontal = vectorAction[0];
        float vertical = vectorAction[1];

        var lastPos = transform.position;
        MoveCar(horizontal, vertical, Time.fixedDeltaTime);

        int reward = GetTrackIncrement();

        var moveVec = transform.position - lastPos;
        float angle = Vector3.Angle(moveVec, _track.forward);
        float bonus = (1f - angle / 90f) * Mathf.Clamp01(vertical) * Time.fixedDeltaTime;
        AddReward(bonus);

        score += reward;
    }

    public override void Heuristic(float[] action)
    {
        action[0] = Input.GetAxis("Horizontal");
        action[1] = Input.GetAxis("Vertical");
    }

    public override void CollectObservations(VectorSensor vectorSensor)
    {
        float angle = Vector3.SignedAngle(_track.forward, transform.forward, Vector3.up);

        vectorSensor.AddObservation(angle / 180f);
        // old  rays
        //vectorSensor.AddObservation(ObserveRay(1.5f, .5f, 25f));
        //vectorSensor.AddObservation(ObserveRay(1.5f, 0f, 0f));
        //vectorSensor.AddObservation(ObserveRay(1.5f, -.5f, -25f));
        //vectorSensor.AddObservation(ObserveRay(-1.5f, 0, 180f));

        // additional rays originating at the edge of the car collision box with angles relative to the car's centroid
        vectorSensor.AddObservation(ObserveRay(0f, 0.75f, (float)Math.Atan(0.75f / 0f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(1.5f, 0.75f, (float)Math.Atan(0.75f / 1.5f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(1.5f, 0.5f, (float)Math.Atan(0.5f / 1.5f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(1.5f, 0f, (float)Math.Atan(0f / 1.5f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(1.5f, -0.5f, (float)Math.Atan(-0.5f / 1.5f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(1.5f, -0.75f, (float)Math.Atan(-0.75f / 1.5f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(0f, -0.75f, (float)Math.Atan(-0.75f / 0f) * (180f / ((float)Math.PI))));
        vectorSensor.AddObservation(ObserveRay(-1.5f, 0f, (float)Math.Atan(-0f / 1.5f) * (180f / ((float)Math.PI)) - 180f));
    }

    private float ObserveRay(float z, float x, float angle)
    {
        var tf = transform;

        // Get the start position of the ray
        var raySource = tf.position + Vector3.up / 2f;
        const float RAY_DIST = 5f;
        var position = raySource + tf.forward * z + tf.right * x;

        // Get the angle of the ray
        var eulerAngle = Quaternion.Euler(0, angle, 0f);
        var dir = eulerAngle * tf.forward;

        // Assign the color for the ray
        var rayColorHit = Color.red;

        // See if there is a hit in the given direction
        Physics.Raycast(position, dir, out var hit, RAY_DIST);

        var retval = 0f;

        //return hit.distance >= 0 ? hit.distance / RAY_DIST : -1f;
        if (hit.distance <= 0)
        {
            retval = hit.distance / RAY_DIST;
            Debug.DrawRay(position, dir * RAY_DIST, Color.red);
        }
        else if (hit.distance > 0)
        {
            retval = -1f;
            Debug.DrawRay(position, dir * RAY_DIST, Color.yellow);
        }

        //Debug.Log(hit.distance);
        return retval;
    }

    private int GetTrackIncrement()
    {
        int reward = 0;
        var carCenter = transform.position + Vector3.up;

        // Find what tile I'm on
        if (Physics.Raycast(carCenter, Vector3.down, out var hit, 2f))
        {
            var newHit = hit.transform;
            // Check if the tile has changed
            if (_track != null && newHit != _track)
            {
                float angle = Vector3.Angle(_track.forward, newHit.position - _track.position);
                reward = (angle < 90f) ? 1 : -1;
            }

            _track = newHit;
        }

        return reward;
    }

    public override void OnEpisodeBegin()
    {
        if (resetOnCollision)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        // Create a small green ray at any collision in normal direction to the collision surface that lasts for about 2 seconds
        Debug.DrawRay(other.contacts[0].point, other.contacts[0].normal, Color.green, 2, false);

        if (other.gameObject.CompareTag("wall"))
        {
            SetReward(-1f);
            EndEpisode();
        }
        // Add to set penalty for hitting the obstacle
        else if (other.gameObject.CompareTag("teapot"))
        {
            SetReward(-0.5f);
        }
    }
}