namespace GorillaLocomotion
{
    using UnityEngine;

    public class Player : MonoBehaviour
    {
        private static Player _instance;

        public static Player Instance { get { return _instance; } }

        public SphereCollider headCollider;
        public CapsuleCollider bodyCollider;

        public Transform leftHandFollower;
        public Transform rightHandFollower;

        public Transform rightHandTransform;
        public Transform leftHandTransform;

        private Vector3 lastLeftHandPosition;
        private Vector3 lastRightHandPosition;
        private Vector3 lastHeadPosition;

        private Rigidbody playerRigidBody;

        public int velocityHistorySize;
        public float maxArmLength = 1.5f;
        public float unStickDistance = 1f;
        public float teleportHandPenetrationTolerance = 0.015f;

        public float velocityLimit;
        public float maxJumpSpeed;
        public float jumpMultiplier;
        public float minimumRaycastDistance = 0.05f;
        public float defaultSlideFactor = 0.03f;
        public float defaultPrecision = 0.995f;

        private Vector3[] velocityHistory;
        private int velocityIndex;
        private Vector3 currentVelocity;
        private Vector3 denormalizedVelocityAverage;
        private bool jumpHandIsLeft;
        private Vector3 lastPosition;
        private bool leftHandBlockedAfterTeleport;
        private bool rightHandBlockedAfterTeleport;

        public Vector3 rightHandOffset;
        public Vector3 leftHandOffset;

        public LayerMask locomotionEnabledLayers;

        public bool wasLeftHandTouching;
        public bool wasRightHandTouching;

        public bool disableMovement = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            else
            {
                _instance = this;
            }
            InitializeValues();
        }

        public void InitializeValues()
        {
            playerRigidBody = GetComponent<Rigidbody>();
            velocityHistorySize = Mathf.Max(1, velocityHistorySize);
            velocityHistory = new Vector3[velocityHistorySize];
            lastLeftHandPosition = leftHandFollower.transform.position;
            lastRightHandPosition = rightHandFollower.transform.position;
            lastHeadPosition = headCollider.transform.position;
            velocityIndex = 0;
            lastPosition = transform.position;
        }

        public void ResetAfterTeleport()
        {
            bodyCollider.transform.eulerAngles = new Vector3(0, headCollider.transform.eulerAngles.y, 0);

            // Never seed Gorilla locomotion from a tracked controller pose that is already
            // inside the floor/wall after spawn, travel, recentering, or a recovery lift.
            // Sweep from the arm root to the tracked pose and start the virtual hand at the
            // first safe contact point instead. A meaningfully displaced hand is temporarily
            // excluded from locomotion until tracking returns it to reachable free space, so
            // a low controller cannot immediately pin or launch the player on the first frame.
            Vector3 leftDesired = CurrentLeftHandPosition();
            Vector3 rightDesired = CurrentRightHandPosition();
            leftHandFollower.position = ResolveHandPositionAfterTeleport(leftDesired, out leftHandBlockedAfterTeleport);
            rightHandFollower.position = ResolveHandPositionAfterTeleport(rightDesired, out rightHandBlockedAfterTeleport);

            InitializeValues();
            currentVelocity = Vector3.zero;
            denormalizedVelocityAverage = Vector3.zero;
            wasLeftHandTouching = false;
            wasRightHandTouching = false;
        }

        private Vector3 ArmRootPosition()
        {
            // Slightly below and behind the head
            return headCollider.transform.position
                 + headCollider.transform.rotation * new Vector3(0f, -0.25f, -0.05f);
        }

        private Vector3 CurrentLeftHandPosition()
        {
            Vector3 armRoot = ArmRootPosition();
            Vector3 desired = PositionWithOffset(leftHandTransform, leftHandOffset);

            if ((desired - armRoot).magnitude <= maxArmLength)
            {
                return desired;
            }
            else
            {
                return armRoot + (desired - armRoot).normalized * maxArmLength;
            }
        }

        private Vector3 CurrentRightHandPosition()
        {
            Vector3 armRoot = ArmRootPosition();
            Vector3 desired = PositionWithOffset(rightHandTransform, rightHandOffset);

            if ((desired - armRoot).magnitude <= maxArmLength)
            {
                return desired;
            }
            else
            {
                return armRoot + (desired - armRoot).normalized * maxArmLength;
            }
        }

        private Vector3 ResolveHandPositionAfterTeleport(Vector3 desiredPosition, out bool blocked)
        {
            blocked = false;
            Vector3 armRoot = ArmRootPosition();
            Vector3 movement = desiredPosition - armRoot;
            if (movement.sqrMagnitude <= 0.000001f)
                return desiredPosition;

            if (!CollisionsSphereCast(
                    armRoot,
                    minimumRaycastDistance,
                    movement,
                    defaultPrecision,
                    out Vector3 safePosition,
                    out _))
            {
                return desiredPosition;
            }

            float tolerance = Mathf.Max(0.0025f, teleportHandPenetrationTolerance);
            blocked = (safePosition - desiredPosition).sqrMagnitude > tolerance * tolerance;
            return safePosition;
        }

        private bool RefreshBlockedHandAfterTeleport(
            ref bool blocked,
            ref Vector3 cachedPosition,
            Transform follower,
            Vector3 desiredPosition)
        {
            if (!blocked)
                return false;

            // Suppress this hand for the whole current frame even if it becomes clear now.
            // That gives the cache one clean frame at the tracked pose before normal Gorilla
            // locomotion is allowed to use the hand as a push/contact source again.
            cachedPosition = ResolveHandPositionAfterTeleport(desiredPosition, out bool stillBlocked);
            follower.position = cachedPosition;
            blocked = stillBlocked;
            return true;
        }

        private Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector)
        {
            return transformToModify.position + transformToModify.rotation * offsetVector;
        }

        private void Update()
        {
            bool leftHandColliding = false;
            bool rightHandColliding = false;
            Vector3 finalPosition;
            Vector3 rigidBodyMovement = Vector3.zero;
            Vector3 firstIterationLeftHand = Vector3.zero;
            Vector3 firstIterationRightHand = Vector3.zero;
            RaycastHit hitInfo;

            bodyCollider.transform.eulerAngles = new Vector3(0, headCollider.transform.eulerAngles.y, 0);

            bool suppressLeftHand = RefreshBlockedHandAfterTeleport(
                ref leftHandBlockedAfterTeleport,
                ref lastLeftHandPosition,
                leftHandFollower,
                CurrentLeftHandPosition());
            bool suppressRightHand = RefreshBlockedHandAfterTeleport(
                ref rightHandBlockedAfterTeleport,
                ref lastRightHandPosition,
                rightHandFollower,
                CurrentRightHandPosition());

            if (suppressLeftHand)
                wasLeftHandTouching = false;
            if (suppressRightHand)
                wasRightHandTouching = false;

            //left hand

            Vector3 distanceTraveled = CurrentLeftHandPosition() - lastLeftHandPosition + Vector3.down * 2f * 9.8f * Time.deltaTime * Time.deltaTime;

            if (!suppressLeftHand && IterativeCollisionSphereCast(lastLeftHandPosition, minimumRaycastDistance, distanceTraveled, defaultPrecision, out finalPosition, true))
            {
                //this lets you stick to the position you touch, as long as you keep touching the surface this will be the zero point for that hand
                if (wasLeftHandTouching)
                {
                    firstIterationLeftHand = lastLeftHandPosition - CurrentLeftHandPosition();
                }
                else
                {
                    firstIterationLeftHand = finalPosition - CurrentLeftHandPosition();
                }
                playerRigidBody.velocity = Vector3.zero;

                leftHandColliding = true;
            }

            //right hand

            distanceTraveled = CurrentRightHandPosition() - lastRightHandPosition + Vector3.down * 2f * 9.8f * Time.deltaTime * Time.deltaTime;

            if (!suppressRightHand && IterativeCollisionSphereCast(lastRightHandPosition, minimumRaycastDistance, distanceTraveled, defaultPrecision, out finalPosition, true))
            {
                if (wasRightHandTouching)
                {
                    firstIterationRightHand = lastRightHandPosition - CurrentRightHandPosition();
                }
                else
                {
                    firstIterationRightHand = finalPosition - CurrentRightHandPosition();
                }

                playerRigidBody.velocity = Vector3.zero;

                rightHandColliding = true;
            }

            //average or add

            if ((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))
            {
                //this lets you grab stuff with both hands at the same time
                rigidBodyMovement = (firstIterationLeftHand + firstIterationRightHand) / 2;
            }
            else
            {
                rigidBodyMovement = firstIterationLeftHand + firstIterationRightHand;
            }

            //check valid head movement

            if (IterativeCollisionSphereCast(lastHeadPosition, headCollider.radius, headCollider.transform.position + rigidBodyMovement - lastHeadPosition, defaultPrecision, out finalPosition, false))
            {
                rigidBodyMovement = finalPosition - lastHeadPosition;
                //last check to make sure the head won't phase through geometry
                if (Physics.Raycast(lastHeadPosition, headCollider.transform.position - lastHeadPosition + rigidBodyMovement, out hitInfo, (headCollider.transform.position - lastHeadPosition + rigidBodyMovement).magnitude + headCollider.radius * defaultPrecision * 0.999f, locomotionEnabledLayers.value))
                {
                    rigidBodyMovement = lastHeadPosition - headCollider.transform.position;
                }
            }

            if (rigidBodyMovement != Vector3.zero)
            {
                transform.position = transform.position + rigidBodyMovement;
            }

            lastHeadPosition = headCollider.transform.position;

            //do final left hand position

            if (suppressLeftHand)
            {
                lastLeftHandPosition = ResolveHandPositionAfterTeleport(
                    CurrentLeftHandPosition(),
                    out leftHandBlockedAfterTeleport);
                leftHandColliding = false;
            }
            else
            {
                distanceTraveled = CurrentLeftHandPosition() - lastLeftHandPosition;

                if (IterativeCollisionSphereCast(lastLeftHandPosition, minimumRaycastDistance, distanceTraveled, defaultPrecision, out finalPosition, !((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))))
                {
                    lastLeftHandPosition = finalPosition;
                    leftHandColliding = true;
                }
                else
                {
                    lastLeftHandPosition = CurrentLeftHandPosition();
                }
            }

            //do final right hand position

            if (suppressRightHand)
            {
                lastRightHandPosition = ResolveHandPositionAfterTeleport(
                    CurrentRightHandPosition(),
                    out rightHandBlockedAfterTeleport);
                rightHandColliding = false;
            }
            else
            {
                distanceTraveled = CurrentRightHandPosition() - lastRightHandPosition;

                if (IterativeCollisionSphereCast(lastRightHandPosition, minimumRaycastDistance, distanceTraveled, defaultPrecision, out finalPosition, !((leftHandColliding || wasLeftHandTouching) && (rightHandColliding || wasRightHandTouching))))
                {
                    lastRightHandPosition = finalPosition;
                    rightHandColliding = true;
                }
                else
                {
                    lastRightHandPosition = CurrentRightHandPosition();
                }
            }

            StoreVelocities();

            if ((rightHandColliding || leftHandColliding) && !disableMovement)
            {
                if (denormalizedVelocityAverage.magnitude > velocityLimit)
                {
                    if (denormalizedVelocityAverage.magnitude * jumpMultiplier > maxJumpSpeed)
                    {
                        playerRigidBody.velocity = denormalizedVelocityAverage.normalized * maxJumpSpeed;
                    }
                    else
                    {
                        playerRigidBody.velocity = jumpMultiplier * denormalizedVelocityAverage;
                    }
                }
            }

            //check to see if left hand is stuck and we should unstick it

            if (leftHandColliding && (CurrentLeftHandPosition() - lastLeftHandPosition).magnitude > unStickDistance && !Physics.SphereCast(headCollider.transform.position, minimumRaycastDistance * defaultPrecision, CurrentLeftHandPosition() - headCollider.transform.position, out hitInfo, (CurrentLeftHandPosition() - headCollider.transform.position).magnitude - minimumRaycastDistance, locomotionEnabledLayers.value))
            {
                lastLeftHandPosition = CurrentLeftHandPosition();
                leftHandColliding = false;
            }

            //check to see if right hand is stuck and we should unstick it

            if (rightHandColliding && (CurrentRightHandPosition() - lastRightHandPosition).magnitude > unStickDistance && !Physics.SphereCast(headCollider.transform.position, minimumRaycastDistance * defaultPrecision, CurrentRightHandPosition() - headCollider.transform.position, out hitInfo, (CurrentRightHandPosition() - headCollider.transform.position).magnitude - minimumRaycastDistance, locomotionEnabledLayers.value))
            {
                lastRightHandPosition = CurrentRightHandPosition();
                rightHandColliding = false;
            }

            leftHandFollower.position = lastLeftHandPosition;
            rightHandFollower.position = lastRightHandPosition;

            wasLeftHandTouching = leftHandColliding;
            wasRightHandTouching = rightHandColliding;
        }

        private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 endPosition, bool singleHand)
        {
            RaycastHit hitInfo;
            Vector3 movementToProjectedAboveCollisionPlane;
            Surface gorillaSurface;
            float slipPercentage;
            //first spherecast from the starting position to the final position
            if (CollisionsSphereCast(startPosition, sphereRadius * precision, movementVector, precision, out endPosition, out hitInfo))
            {
                //if we hit a surface, do a bit of a slide. this makes it so if you grab with two hands you don't stick 100%, and if you're pushing along a surface while braced with your head, your hand will slide a bit

                //take the surface normal that we hit, then along that plane, do a spherecast to a position a small distance away to account for moving perpendicular to that surface
                Vector3 firstPosition = endPosition;
                gorillaSurface = hitInfo.collider.GetComponent<Surface>();
                slipPercentage = gorillaSurface != null ? gorillaSurface.slipPercentage : (!singleHand ? defaultSlideFactor : 0.001f);
                movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, hitInfo.normal) * slipPercentage;
                if (CollisionsSphereCast(endPosition, sphereRadius, movementToProjectedAboveCollisionPlane, precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit trying to move perpendicularly, stop there and our end position is the final spot we hit
                    return true;
                }
                //if not, try to move closer towards the true point to account for the fact that the movement along the normal of the hit could have moved you away from the surface
                else if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), precision * precision * precision, out endPosition, out hitInfo))
                {
                    //if we hit, then return the spot we hit
                    return true;
                }
                else
                {
                    //this shouldn't really happe, since this means that the sliding motion got you around some corner or something and let you get to your final point. back off because something strange happened, so just don't do the slide
                    endPosition = firstPosition;
                    return true;
                }
            }
            //as kind of a sanity check, try a smaller spherecast. this accounts for times when the original spherecast was already touching a surface so it didn't trigger correctly
            else if (CollisionsSphereCast(startPosition, sphereRadius * precision * 0.66f, movementVector.normalized * (movementVector.magnitude + sphereRadius * precision * 0.34f), precision * 0.66f, out endPosition, out hitInfo))
            {
                endPosition = startPosition;
                return true;
            }
            else
            {
                endPosition = Vector3.zero;
                return false;
            }
        }

        private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, float precision, out Vector3 finalPosition, out RaycastHit hitInfo)
        {
            //kind of like a souped up spherecast. includes checks to make sure that the sphere we're using, if it touches a surface, is pushed away the correct distance (the original sphereradius distance). since you might
            //be pushing into sharp corners, this might not always be valid, so that's what the extra checks are for

            //initial spherecase
            RaycastHit innerHit;
            if (Physics.SphereCast(startPosition, sphereRadius * precision, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * (1 - precision), locomotionEnabledLayers.value))
            {
                //if we hit, we're trying to move to a position a sphereradius distance from the normal
                finalPosition = hitInfo.point + hitInfo.normal * sphereRadius;

                //check a spherecase from the original position to the intended final position
                if (Physics.SphereCast(startPosition, sphereRadius * precision * precision, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * (1 - precision * precision), locomotionEnabledLayers.value))
                {
                    finalPosition = startPosition + (finalPosition - startPosition).normalized * Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision));
                    hitInfo = innerHit;
                }
                //bonus raycast check to make sure that something odd didn't happen with the spherecast. helps prevent clipping through geometry
                else if (Physics.Raycast(startPosition, finalPosition - startPosition, out innerHit, (finalPosition - startPosition).magnitude + sphereRadius * precision * precision * 0.999f, locomotionEnabledLayers.value))
                {
                    finalPosition = startPosition;
                    hitInfo = innerHit;
                    return true;
                }
                return true;
            }
            //anti-clipping through geometry check
            else if (Physics.Raycast(startPosition, movementVector, out hitInfo, movementVector.magnitude + sphereRadius * precision * 0.999f, locomotionEnabledLayers.value))
            {
                finalPosition = startPosition;
                return true;
            }
            else
            {
                finalPosition = Vector3.zero;
                return false;
            }
        }

        public bool IsHandTouching(bool forLeftHand)
        {
            if (forLeftHand)
            {
                return wasLeftHandTouching;
            }
            else
            {
                return wasRightHandTouching;
            }
        }

        public void Turn(float degrees)
        {
            transform.RotateAround(headCollider.transform.position, transform.up, degrees);
            denormalizedVelocityAverage = Quaternion.Euler(0, degrees, 0) * denormalizedVelocityAverage;
            for (int i = 0; i < velocityHistory.Length; i++)
            {
                velocityHistory[i] = Quaternion.Euler(0, degrees, 0) * velocityHistory[i];
            }
        }

        private void StoreVelocities()
        {
            if (Time.deltaTime <= 0f)
            {
                lastPosition = transform.position;
                return;
            }
            velocityIndex = (velocityIndex + 1) % velocityHistory.Length;
            Vector3 oldestVelocity = velocityHistory[velocityIndex];
            currentVelocity = (transform.position - lastPosition) / Time.deltaTime;
            denormalizedVelocityAverage += (currentVelocity - oldestVelocity) / (float)velocityHistory.Length;
            velocityHistory[velocityIndex] = currentVelocity;
            lastPosition = transform.position;
        }
    }
}
