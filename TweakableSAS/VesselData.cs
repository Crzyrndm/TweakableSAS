using System;
using UnityEngine;

namespace TweakableSAS
{
    public class VesselData
    {
        public VesselData()
        {
            Instance = this;
        }

        public static VesselData Instance;
        Vector3 vesselFacingAxis = new Vector3();
        public double radarAlt { get; private set; }
        public double pitch { get; private set; }
        public double bank { get; private set; }
        public double yaw { get; private set; }
        public double AoA { get; private set; }
        public double heading { get; private set; }
        public double progradeHeading { get; private set; }
        public double vertSpeed { get; private set; }
        public double acceleration { get; private set; }
        public Vector3d planetUp { get; private set; }
        public Vector3d planetNorth { get; private set; }
        public Vector3d planetEast { get; private set; }
        public Vector3d surfVelForward { get; private set; }
        public Vector3d surfVelRight { get; private set; }
        public Vector3d surfVesForward { get; private set; }
        public Vector3d surfVesRight { get; private set; }
        public Vector3d lastVelocity { get; private set; }
        public Vector3d velocity { get; private set; }
        public Vector3 obtRadial { get; private set; }
        public Vector3 obtNormal { get; private set; }
        public Vector3 srfRadial { get; private set; }
        public Vector3 srfNormal { get; private set; }

        /// <summary>
        /// Call in OnPreAutoPilotUpdate. Do not call multiple times per physics frame or the "lastPlanetUp" vector will not be correct and VSpeed will not be calculated correctly
        /// Can't just leave it to a Coroutine becuase it has to be called before anything else
        /// </summary>
        public void updateAttitude()
        {
            vesselFacingAxis = FlightGlobals.ActiveVessel.transform.up;
            planetUp = (FlightGlobals.ActiveVessel.rootPart.transform.position - FlightGlobals.ActiveVessel.mainBody.position).normalized;
            planetEast = FlightGlobals.ActiveVessel.mainBody.getRFrmVel(FlightGlobals.ActiveVessel.findWorldCenterOfMass()).normalized;
            planetNorth = Vector3d.Cross(planetEast, planetUp).normalized;

            // 4 frames of reference to use. Orientation, Velocity, and both of the previous parallel to the surface
            radarAlt = FlightGlobals.ActiveVessel.altitude - (FlightGlobals.ActiveVessel.mainBody.ocean ? Math.Max(FlightGlobals.ActiveVessel.pqsAltitude, 0) : FlightGlobals.ActiveVessel.pqsAltitude);
            velocity = FlightGlobals.ActiveVessel.rootPart.Rigidbody.velocity + Krakensbane.GetFrameVelocity();
            acceleration = (velocity - lastVelocity).magnitude / TimeWarp.fixedDeltaTime;
            acceleration *= Math.Sign(Vector3.Dot(velocity - lastVelocity, velocity));
            vertSpeed = Vector3d.Dot(planetUp, (velocity + lastVelocity) / 2);
            lastVelocity = velocity;

            // Velocity forward and right vectors parallel to the surface
            surfVelRight = Vector3d.Cross(planetUp, FlightGlobals.ActiveVessel.srf_velocity).normalized;
            surfVelForward = Vector3d.Cross(surfVelRight, planetUp).normalized;

            // Vessel forward and right vectors parallel to the surface
            surfVesRight = Vector3d.Cross(planetUp, vesselFacingAxis).normalized;
            surfVesForward = Vector3d.Cross(surfVesRight, planetUp).normalized;

            obtNormal = Vector3.Cross(FlightGlobals.ActiveVessel.obt_velocity, planetUp).normalized;
            obtRadial = Vector3.Cross(FlightGlobals.ActiveVessel.obt_velocity, obtNormal).normalized;
            srfNormal = Vector3.Cross(FlightGlobals.ActiveVessel.srf_velocity, planetUp).normalized;
            srfRadial = Vector3.Cross(FlightGlobals.ActiveVessel.srf_velocity, srfNormal).normalized;

            pitch = 90 - Vector3d.Angle(planetUp, vesselFacingAxis);
            heading = headingClamp(Vector3d.Angle(surfVesForward, planetNorth) * Math.Sign(Vector3d.Dot(surfVesForward, planetEast)), 360);
            progradeHeading = headingClamp(Vector3d.Angle(surfVelForward, planetNorth) * Math.Sign(Vector3d.Dot(surfVelForward, planetEast)), 360);
            bank = Vector3d.Angle(surfVesRight, FlightGlobals.ActiveVessel.ReferenceTransform.right) * Math.Sign(Vector3d.Dot(surfVesRight, -FlightGlobals.ActiveVessel.ReferenceTransform.forward));

            if (FlightGlobals.ActiveVessel.srfSpeed > 1)
            {
                Vector3d AoAVec = FlightGlobals.ActiveVessel.srf_velocity.projectOnPlane(FlightGlobals.ActiveVessel.ReferenceTransform.right);
                AoA = Vector3d.Angle(AoAVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(AoAVec, FlightGlobals.ActiveVessel.ReferenceTransform.forward));

                Vector3d yawVec = FlightGlobals.ActiveVessel.srf_velocity.projectOnPlane(FlightGlobals.ActiveVessel.ReferenceTransform.forward);
                yaw = Vector3d.Angle(yawVec, vesselFacingAxis) * Math.Sign(Vector3d.Dot(yawVec, FlightGlobals.ActiveVessel.ReferenceTransform.right));
            }
            else
                AoA = yaw = 0;
        }

        /// <summary>
        /// Circular rounding to keep compass measurements within a 360 degree range
        /// maxHeading is the top limit, bottom limit is maxHeading - 360
        /// </summary>
        public static double headingClamp(double valToClamp, double maxHeading, double range = 360)
        {
            double temp = (valToClamp - (maxHeading - range)) % range;
            return (maxHeading - range) + (temp < 0 ? temp + range : temp);
        }
    }
}
