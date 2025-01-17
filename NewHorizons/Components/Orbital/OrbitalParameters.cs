﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Logger = NewHorizons.Utility.Logger;

namespace NewHorizons.Components.Orbital
{
    public class OrbitalParameters : IOrbitalParameters
    {
        public float Inclination { get; set; }
        public float SemiMajorAxis { get; set; }
        public float LongitudeOfAscendingNode { get; set; }
        public float Eccentricity { get; set; }
        public float ArgumentOfPeriapsis { get; set; }
        public float TrueAnomaly { get; set; }
        public float Period { get; set; }

        public Vector3 InitialPosition { get; private set; }
        public Vector3 InitialVelocity { get; private set; }


        public static OrbitalParameters FromTrueAnomaly(Gravity primaryGravity, Gravity secondaryGravity, float eccentricity, float semiMajorAxis, float inclination, float argumentOfPeriapsis, float longitudeOfAscendingNode, float trueAnomaly)
        {
            var orbitalParameters = new OrbitalParameters();
            orbitalParameters.Inclination = inclination;   
            orbitalParameters.SemiMajorAxis = semiMajorAxis;
            orbitalParameters.LongitudeOfAscendingNode = longitudeOfAscendingNode;
            orbitalParameters.Eccentricity = eccentricity;
            orbitalParameters.ArgumentOfPeriapsis = argumentOfPeriapsis;    

            // If primary gravity is linear and the orbit is eccentric its not even an ellipse so theres no true anomaly
            if(primaryGravity.Power == 1 && eccentricity != 0)
            {
                trueAnomaly = 0;
            }

            orbitalParameters.TrueAnomaly = trueAnomaly;

            // Have to calculate the rest

            var primaryMass = primaryGravity.Mass;
            var secondaryMass = secondaryGravity.Mass;

            var power = primaryGravity.Power;
            var period = (float) (GravityVolume.GRAVITATIONAL_CONSTANT * (primaryMass + secondaryMass) / (4 * Math.PI * Math.PI * Math.Pow(semiMajorAxis, power)));

            orbitalParameters.Period = period;

            // All in radians
            var f = Mathf.Deg2Rad * trueAnomaly;
            var p = semiMajorAxis * (1 - (eccentricity * eccentricity)); // Semi-latus rectum
            var r = p / (1 + eccentricity * Mathf.Cos(f));

            var G = GravityVolume.GRAVITATIONAL_CONSTANT;
            var mu = G * (primaryMass);

            var r_p = semiMajorAxis * (1 - eccentricity);
            var r_a = semiMajorAxis * (1 + eccentricity);

            float v;

            // For linear
            if(primaryGravity.Power == 1)
            {
                // Have to deal with a limit
                if(eccentricity == 0)
                {
                    var v2 = 2 * mu * (Mathf.Log(r_a / r) + (1 / 2f));
                    v = Mathf.Sqrt(v2);
                }
                else
                {
                    var coeff = (r_p * r_p) / (r_p * r_p - r_a * r_a);
                    var v2 = 2 * mu * (coeff * Mathf.Log(r_p / r_a) + Mathf.Log(r_a / r));
                    v = Mathf.Sqrt(v2);
                }
            }
            // For inverseSquare
            else
            {
                v = Mathf.Sqrt(G * primaryMass * ((2f / r) - (1f / semiMajorAxis)));
            }

            // Origin is the focus
            var x = r * Mathf.Cos(f);
            var y = r * Mathf.Sin(f);

            // Forgot to take the derivative of r as well with respect to f
            var denominator = Mathf.Pow(eccentricity * Mathf.Cos(f) + 1, 2);
            var dx = -p * Mathf.Sin(f) / denominator;
            var dy = p * (eccentricity + Mathf.Cos(f)) / denominator;

            var dir = Rotate(new Vector3(x, 0f, y).normalized, longitudeOfAscendingNode, inclination, argumentOfPeriapsis);
            var velocityDir = Rotate(new Vector3(dx, 0f, dy).normalized, longitudeOfAscendingNode, inclination, argumentOfPeriapsis);

            var pos = r * dir;
            var vel = v * velocityDir;

            orbitalParameters.InitialPosition = pos;
            orbitalParameters.InitialVelocity = vel;

            return orbitalParameters;
        }

        public static Vector3 Rotate(Vector3 vector, float longitudeOfAscendingNode, float inclination, float argumentOfPeriapsis)
        {
            var R1 = Quaternion.AngleAxis(longitudeOfAscendingNode + argumentOfPeriapsis, Vector3.up);
            var R2 = Quaternion.AngleAxis(inclination, R1 * Vector3.left);

            return R1 * R2 * vector;
        }

        public OrbitalParameters GetOrbitalParameters(Gravity primaryGravity, Gravity secondaryGravity)
        {
            return FromTrueAnomaly(primaryGravity, secondaryGravity, Eccentricity, SemiMajorAxis, Inclination, ArgumentOfPeriapsis, LongitudeOfAscendingNode, TrueAnomaly);
        }
    }
}
