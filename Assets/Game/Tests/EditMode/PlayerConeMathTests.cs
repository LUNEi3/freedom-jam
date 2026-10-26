using FreedomJam.Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace FreedomJam.Game.Tests
{
    public class PlayerConeMathTests
    {
        static bool InCone(Vector3 target, float range = 2f, float angle = 90f) =>
            ConeMath.IsInCone(Vector3.zero, Vector3.forward, target, range, angle);

        [Test] public void TargetStraightAhead_InRange_IsInside()
        {
            Assert.That(InCone(new Vector3(0, 0, 1.5f)), Is.True);
        }
        [Test] public void TargetBeyondRangeOrBehind_IsOutside()
        {
            Assert.That(InCone(new Vector3(0, 0, 2.5f)), Is.False);
            Assert.That(InCone(new Vector3(0, 0, -1f)), Is.False);
        }
        [Test] public void AngleEdge_InsideAtHalfAngle_OutsideJustPast()
        {
            // 90 degree cone = 45 degrees each side.
            Assert.That(InCone(new Vector3(1, 0, 1).normalized), Is.True);
            Assert.That(InCone(Quaternion.Euler(0, 46, 0) * Vector3.forward), Is.False);
        }
        [Test] public void HeightDifference_IsIgnored()
        {
            Assert.That(InCone(new Vector3(0, 10f, 1f)), Is.True);
        }
        [Test] public void ZeroRange_IsAlwaysOutside()
        {
            Assert.That(InCone(Vector3.zero, range: 0f), Is.False);
        }

        static bool CircleInCone(Vector3 target, float radius, float range = 2f, float angle = 90f) =>
            ConeMath.IsInCone(Vector3.zero, Vector3.forward, target, range, angle, radius);

        [Test] public void TargetRadius_EdgeInsideRange_CountsEvenIfCentreIsOut()
        {
            var centreJustOut = new Vector3(0, 0, 2.4f);
            Assert.That(InCone(centreJustOut), Is.False);
            Assert.That(CircleInCone(centreJustOut, 0.5f), Is.True);
            Assert.That(CircleInCone(new Vector3(0, 0, 2.6f), 0.5f), Is.False);
        }
        [Test] public void TargetRadius_EdgeInsideAngle_CountsEvenIfCentreIsOutside()
        {
            var centreAt50Degrees = Quaternion.Euler(0, 50, 0) * Vector3.forward * 1.5f;
            Assert.That(InCone(centreAt50Degrees), Is.False);
            Assert.That(CircleInCone(centreAt50Degrees, 0.5f), Is.True);
        }
        [Test] public void TargetRadius_FarBehind_IsOutside()
        {
            Assert.That(CircleInCone(new Vector3(0, 0, -1.5f), 0.5f), Is.False);
        }
        [Test] public void TargetRadius_OverlappingMouth_IsInside()
        {
            Assert.That(CircleInCone(new Vector3(0, 0, -0.3f), 0.5f), Is.True);
        }
    }
}
