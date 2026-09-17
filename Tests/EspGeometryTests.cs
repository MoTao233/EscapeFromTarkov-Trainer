using System;
using EFT.Trainer.Features;
using UnityEngine;

internal static class EspGeometryTests
{
	private static int _assertions;
	private static void Check(bool value, string message)
	{
		_assertions++;
		if (!value) throw new Exception(message);
	}
	private static bool Close(float a, float b) => Math.Abs(a - b) < 0.002f;
	private static bool Clip(ref Vector2 a, ref Vector2 b, bool circle = false, float radius = 25) =>
		EspGeometry.ClipLine(ref a, ref b, new Rect(0, 0, 100, 100), circle, new Vector2(50, 50), radius);

	private static void Main()
	{
		var a = new Vector2(-20, 50); var b = new Vector2(120, 50);
		Check(Clip(ref a, ref b) && Close(a.x, 0) && Close(b.x, 100), "Cross-screen line must be clipped, not discarded.");
		a = new Vector2(50, -20); b = new Vector2(50, 120);
		Check(Clip(ref a, ref b) && Close(a.y, 0) && Close(b.y, 100), "Vertical screen-edge clipping.");
		a = new Vector2(120, 50); b = new Vector2(-20, 50);
		Check(Clip(ref a, ref b) && Close(a.x, 100) && Close(b.x, 0), "Reversed direction must preserve endpoint order.");
		a = new Vector2(-30, -10); b = new Vector2(130, -10);
		Check(!Clip(ref a, ref b), "Fully offscreen line must be hidden.");
		a = new Vector2(10, 10); b = a;
		Check(!Clip(ref a, ref b), "Coincident joints must not produce invalid GUI rotation.");
		a = new Vector2(-10, 50); b = new Vector2(110, 50);
		Check(Clip(ref a, ref b, true) && Close(a.x, 25) && Close(b.x, 75), "Scope chord must end at lens boundary.");
		a = new Vector2(0, 0); b = new Vector2(100, 0);
		Check(!Clip(ref a, ref b, true), "Line outside optic must not be clamped onto its rim.");
		a = new Vector2(45, 50); b = new Vector2(55, 50);
		Check(Clip(ref a, ref b, true) && Close(a.x, 45) && Close(b.x, 55), "Inside optic coordinates must remain unchanged.");
		a = new Vector2(0, 25); b = new Vector2(100, 25);
		Check(!Clip(ref a, ref b, true), "A tangent reduces to a point, not a visible line.");
		a = new Vector2(float.NaN, 10); b = new Vector2(20, 20);
		Check(!Clip(ref a, ref b), "NaN must not reach GUI.");
		a = new Vector2(10, 10); b = new Vector2(float.PositiveInfinity, 20);
		Check(!Clip(ref a, ref b), "Infinity must not reach GUI.");
		a = new Vector2(40, 40); b = new Vector2(60, 60);
		Check(!Clip(ref a, ref b, true, 0), "Uninitialized lens must be rejected.");
		Check(Close(EspGeometry.HealthFraction(50, 100), .5f), "Health fraction.");
		Check(EspGeometry.HealthFraction(-10, 100) == 0, "Negative health.");
		Check(EspGeometry.HealthFraction(120, 100) == 1, "Overhealed health.");
		Check(EspGeometry.HealthFraction(100, 0) == 0, "Missing maximum health.");
		Check(EspGeometry.HealthFraction(float.NaN, 100) == 0, "NaN health.");
		var random = new System.Random(35392);
		for (int i = 0; i < 2000; i++)
		{
			a = new Vector2(random.Next(-100, 201), random.Next(-100, 201));
			b = new Vector2(random.Next(-100, 201), random.Next(-100, 201));
			var originalA = a; var originalB = b;
			bool circle = (i & 1) == 0;
			if (!Clip(ref a, ref b, circle)) continue;
			Check(a.x >= -.002f && a.y >= -.002f && a.x <= 100.002f && a.y <= 100.002f &&
				b.x >= -.002f && b.y >= -.002f && b.x <= 100.002f && b.y <= 100.002f, "Clipped points must stay on screen.");
			if (circle) Check((a - new Vector2(50, 50)).sqrMagnitude <= 625.02f &&
				(b - new Vector2(50, 50)).sqrMagnitude <= 625.02f, "Clipped points must stay in lens.");
			var d = originalB - originalA;
			Check(Math.Abs((a.x - originalA.x) * d.y - (a.y - originalA.y) * d.x) < .02f &&
				Math.Abs((b.x - originalA.x) * d.y - (b.y - originalA.y) * d.x) < .02f, "Clipping must not bend skeleton segments.");
		}
		Console.WriteLine($"PASS: {_assertions} assertions (including 2000 deterministic clipping cases).");
	}
}
