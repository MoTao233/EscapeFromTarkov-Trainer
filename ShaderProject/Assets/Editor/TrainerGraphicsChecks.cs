using System;
using System.Collections.Generic;
using System.IO;
using EFT.Trainer.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Render real meshes through the production command-buffer code. These checks catch blend,
// depth, render-target orientation and submesh errors that a C# compilation cannot detect.
public static class TrainerGraphicsChecks
{
    private const int Size = 256;
    private static readonly List<HighlightTarget> Empty = new List<HighlightTarget>();
    private static int _checks;

    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        _checks += TrainerHierarchyChecks.Run();
        var bundle = AssetBundle.LoadFromFile(Path.Combine(TrainerShaderBuild.Output, "trainer-highlights"));
        var mask = bundle.LoadAsset<Shader>("assets/trainer/highlightmask.shader");
        var composite = bundle.LoadAsset<Shader>("assets/trainer/highlightcomposite.shader");
        Require(mask != null && composite != null && mask.isSupported && composite.isSupported, "packaged shaders load and support this device");
        var light = new GameObject("Test light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(30, -30, 0);
        light.intensity = 0.9f;
        var camera = new GameObject("Highlight test camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.08f, 0.08f, 1);
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100;
        camera.fieldOfView = 60;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.depthTextureMode = DepthTextureMode.Depth;
        camera.targetTexture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture.Create();
        var body = Cube("Body", new Vector3(-0.45f, 0.45f, 5), new Vector3(1.4f, 1.8f, 0.8f), Color.gray);
        var original = body.sharedMaterial;
        var target = new HighlightTarget { Fill = new Color(1, 0, 0, 0.25f), Edge = Color.cyan,
            Hidden = new Color(1, 0, 1, 0.5f) };
        target.Refresh(new[] { body });
        var players = new List<HighlightTarget> { target };
        var presenter = camera.gameObject.AddComponent<TrainerSnapshotPresenter>();
        Require(presenter != null, "post-processing presenter attaches as a runtime component");
        using (var renderer = new HighlightRenderer(mask, composite))
        {
            var before = Render(camera);
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            var after = Render(camera);
            Require(presenter.PresentedFrames == 2, "post-processing actually presents both frames");
            Save(after, "postprocessing-capture");
            Require(ChangedPixels(before, after) > 1000, "highlight reaches the post-processing snapshot, not a discarded camera buffer");
            UnityEngine.Object.DestroyImmediate(before);
            UnityEngine.Object.DestroyImmediate(after);
        }
        UnityEngine.Object.DestroyImmediate(presenter);
        CheckViewport(camera, mask, composite, players);
        using (var renderer = new HighlightRenderer(mask, composite))
        {
            foreach (var path in new[] { RenderingPath.Forward, RenderingPath.DeferredShading })
            {
                camera.renderingPath = path;
                renderer.Clear();
                var baseline = Render(camera);
                Require(camera.actualRenderingPath == path, "actual rendering path: " + path);
                renderer.Record(camera, players, Empty, 2, 2, 0, 64);
                var filled = Render(camera);
                Save(filled, "visible-" + path);
                var center = Point(camera, body.bounds.center);
                var expected = Color.Lerp(baseline.GetPixel(center.x, center.y), Color.red, 0.25f);
                var actual = filled.GetPixel(center.x, center.y);
                Require(ColorDistance(expected, actual) < 0.06f, path + ": true alpha fill, preserving original shading; actual=" + actual + " expected=" + expected);
                Require(ChangedPixels(baseline, filled) > 1000, path + ": visible silhouette drawn");
                Require(renderer.DrawCount == 1, path + ": one mask draw for one submesh");
                Require(body.sharedMaterial == original, path + ": original material preserved");

                target.Fill = Color.clear;
                renderer.Record(camera, players, Empty, 2, 2, 0, 64);
                var outline = Render(camera);
                Save(outline, "outline-" + path);
                Require(ColorDistance(outline.GetPixel(center.x, center.y), baseline.GetPixel(center.x, center.y)) < 0.025f, path + ": zero fill leaves interior unchanged");
                Require(ChangedPixels(baseline, outline) > 100, path + ": outline visible independently of fill");
                target.Fill = new Color(1, 0, 0, 0.25f);

                var wall = Cube("Occluder", new Vector3(-0.25f, 0.25f, 3), new Vector3(3, 3, 0.2f), Color.white);
                renderer.Clear();
                var covered = Render(camera);
                renderer.Record(camera, players, Empty, 2, 2, 0, 64);
                var noXRay = Render(camera);
                Save(noXRay, "occluded-" + path);
                Require(ChangedPixels(covered, noXRay) < 4, path + ": occluder hides both outline and fill");
                target.XRay = true;
                renderer.Record(camera, players, Empty, 2, 2, 0, 64);
                var xray = Render(camera);
                Require(ChangedPixels(covered, xray) > 1000, path + ": optional X-ray renders hidden silhouette");
                target.XRay = false;
                UnityEngine.Object.DestroyImmediate(wall.gameObject);
                CheckFenceAndGlass(camera, renderer, players);
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(filled);
                UnityEngine.Object.DestroyImmediate(outline);
                UnityEngine.Object.DestroyImmediate(covered);
                UnityEngine.Object.DestroyImmediate(noXRay);
                UnityEngine.Object.DestroyImmediate(xray);
            }

            // A second layer exercises state restoration after the first layer's full-screen blit.
            var loot = Cube("Loot", new Vector3(1, -0.75f, 4), Vector3.one * 0.5f, Color.gray);
            var item = new HighlightTarget { Fill = new Color(0, 0, 1, 0.4f), Edge = Color.yellow };
            item.Refresh(new[] { loot });
            renderer.Clear();
            var bothBaseline = Render(camera);
            renderer.Record(camera, players, new[] { item }, 2, 2, 0, 64);
            var both = Render(camera);
            Save(both, "characters-and-loot");
            var bodyPoint = Point(camera, body.bounds.center);
            var lootPoint = Point(camera, loot.bounds.center);
            Require(ColorDistance(both.GetPixel(bodyPoint.x, bodyPoint.y), bothBaseline.GetPixel(bodyPoint.x, bodyPoint.y)) > 0.1f, "two layers: character fill");
            Require(ColorDistance(both.GetPixel(lootPoint.x, lootPoint.y), bothBaseline.GetPixel(lootPoint.x, lootPoint.y)) > 0.1f, "two layers: loot fill");
            Require(renderer.TargetCount == 2, "two layers: shared camera buffer");

            // Multiple material slots must draw every submesh, not just the last one.
            var filter = body.GetComponent<MeshFilter>();
            var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
            var triangles = mesh.triangles;
            int split = triangles.Length / 2;
            var first = new int[split]; var second = new int[triangles.Length - split];
            Array.Copy(triangles, 0, first, 0, first.Length);
            Array.Copy(triangles, split, second, 0, second.Length);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(first, 0); mesh.SetTriangles(second, 1);
            filter.sharedMesh = mesh;
            body.sharedMaterials = new[] { original, original };
            target.Refresh(new[] { body });
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            Require(renderer.DrawCount == 2, "all material submeshes are drawn");
            Render(camera);

            target.MaximumDistance = 1;
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            Require(renderer.DrawCount == 0, "distance culling");
            target.MaximumDistance = 0;
            body.transform.position = new Vector3(100, 0, 5);
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            Require(renderer.DrawCount == 0, "frustum culling");
            body.transform.position = new Vector3(2, 0, 5);
            renderer.Record(camera, players, Empty, 2, 2, 1, 64);
            Require(renderer.DrawCount == 0, "screen radius culling");
            Require(HighlightGeometry.IntersectsRadius(new Rect(126, 110, 90, 40), Size, Size, 2), "radius intersects bounds even when center lies outside");
            renderer.Record(camera, Empty, new[] { item, item }, 2, 2, 0, 1);
            Require(renderer.TargetCount == 1, "item rendering budget");
            renderer.Detach();
            Require(camera.GetCommandBuffers(HighlightRenderer.Event).Length == 0, "disable/raid cleanup detaches command buffers");

            // Skinned meshes are drawn directly, without baking a fresh CPU mesh each frame.
            loot.gameObject.SetActive(false);
            body.gameObject.SetActive(false);
            var skinnedObject = new GameObject("Skinned target");
            var skinned = skinnedObject.AddComponent<SkinnedMeshRenderer>();
            var bone = new GameObject("Bone").transform;
            bone.parent = skinnedObject.transform;
            skinnedObject.transform.position = new Vector3(0, 0, 4);
            var skinMesh = UnityEngine.Object.Instantiate(mesh);
            var weights = new BoneWeight[skinMesh.vertexCount];
            for (int i = 0; i < weights.Length; i++) weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
            skinMesh.boneWeights = weights;
            skinMesh.bindposes = new[] { Matrix4x4.identity };
            skinned.sharedMesh = skinMesh;
            skinned.sharedMaterials = new[] { original, original };
            skinned.bones = new[] { bone };
            skinned.rootBone = bone;
            skinned.updateWhenOffscreen = true;
            target.Refresh(new[] { skinned });
            var skinBaseline = Render(camera);
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            var skinHighlight = Render(camera);
            Require(renderer.DrawCount == 2 && ChangedPixels(skinBaseline, skinHighlight) > 100, "skinned mesh with multiple submeshes");
            Save(skinHighlight, "skinned-mesh");
            renderer.Detach();
            camera.depthTextureMode = DepthTextureMode.None;
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            Require(camera.depthTextureMode == DepthTextureMode.None, "native depth does not request a shadow-caster depth texture");
            renderer.Detach();
            Require(camera.depthTextureMode == DepthTextureMode.None, "release our depth request on disable");
            camera.depthTextureMode = DepthTextureMode.DepthNormals | DepthTextureMode.Depth;
            renderer.Record(camera, players, Empty, 2, 2, 0, 64);
            renderer.Detach();
            Require(camera.depthTextureMode == (DepthTextureMode.DepthNormals | DepthTextureMode.Depth), "preserve pre-existing depth requests");
            renderer.Record(camera, Empty, Empty, 2, 2, 0, 64);
            Require(camera.GetCommandBuffers(HighlightRenderer.Event).Length == 0, "empty scene does not allocate a camera buffer");
        }
        File.WriteAllText(Path.Combine(TrainerShaderBuild.Output, "graphics-checks.txt"), _checks + " graphics checks passed.\n");
        Debug.Log("TRAINER_GRAPHICS_CHECKS_OK: " + _checks);
    }

    private static void CheckViewport(Camera camera, Shader mask, Shader composite, List<HighlightTarget> players)
    {
        var output = camera.targetTexture;
        camera.targetTexture = null;
        foreach (var rect in new[] { new Rect(0, 0, 0.667f, 0.667f), new Rect(0.1f, 0.15f, 0.65f, 0.65f) })
        {
            camera.rect = rect;
            var presenter = camera.gameObject.AddComponent<TrainerSnapshotPresenter>();
            using (var renderer = new HighlightRenderer(mask, composite))
            {
                camera.Render();
                var baseline = Read(presenter.Snapshot);
                renderer.Record(camera, players, Empty, 2, 2, 0, 64);
                camera.Render();
                var after = Read(presenter.Snapshot);
                Require(presenter.PresentedFrames == 2, "cropped viewport presents both frames: " + rect);
                Require(ChangedPixels(baseline, after) > 100, "cropped viewport highlights reach captured scene: " + rect);
                var p = camera.WorldToViewportPoint(new Vector3(-0.45f, 0.45f, 5));
                int x = (int)(p.x * after.width), y = (int)(p.y * after.height);
                Require(ColorDistance(after.GetPixel(x, y), Color.Lerp(baseline.GetPixel(x, y), Color.red, 0.25f)) < 0.06f,
                    "cropped viewport fill remains aligned: " + rect);
                Save(after, "viewport-" + rect.x);
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(after);
            }
            UnityEngine.Object.DestroyImmediate(presenter);
        }
        camera.rect = new Rect(0, 0, 1, 1);
        camera.targetTexture = output;
    }

    private static void CheckFenceAndGlass(Camera camera, HighlightRenderer renderer, List<HighlightTarget> players)
    {
        var fence = GameObject.CreatePrimitive(PrimitiveType.Quad);
        fence.name = "Alpha-test wire mesh without a shadow pass";
        fence.transform.position = new Vector3(-0.45f, 0.45f, 3);
        fence.transform.localScale = new Vector3(2.8f, 2.8f, 1);
        fence.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Hidden/TrainerChecks/Fence"));
        renderer.Clear();
        var baseline = Render(camera);
        var edge = players[0].Edge;
        players[0].Edge = Color.clear;
        renderer.Record(camera, players, Empty, 2, 2, 0, 64);
        var after = Render(camera);
        int wires = 0, changedWires = 0;
        for (int y = 2; y < Size - 2; y++)
            for (int x = 2; x < Size - 2; x++)
            {
                var pixel = baseline.GetPixel(x, y);
                // Disable the outer outline for this assertion: test geometry visibility alone.
                if (pixel.g < 0.7f || pixel.r > 0.2f || baseline.GetPixel(x - 2, y).g < 0.7f ||
                    baseline.GetPixel(x + 2, y).g < 0.7f || baseline.GetPixel(x, y - 2).g < 0.7f || baseline.GetPixel(x, y + 2).g < 0.7f) continue;
                wires++;
                if (ColorDistance(pixel, after.GetPixel(x, y)) > 0.025f) changedWires++;
            }
        Require(wires > 100 && changedWires == 0, camera.actualRenderingPath + ": solid wire depth preserved without a ShadowCaster pass; wires=" + wires + ", changed=" + changedWires);
        Require(ChangedPixels(baseline, after) > 500, camera.actualRenderingPath + ": fill and outline visible through wire-mesh holes");
        players[0].Edge = edge;
        renderer.Record(camera, players, Empty, 2, 2, 0, 64);
        var outlined = Render(camera);
        Save(outlined, "wire-mesh-" + camera.actualRenderingPath);
        UnityEngine.Object.DestroyImmediate(outlined);
        UnityEngine.Object.DestroyImmediate(fence.gameObject);
        UnityEngine.Object.DestroyImmediate(baseline);
        UnityEngine.Object.DestroyImmediate(after);

        var glass = Cube("Transparent glass", new Vector3(-0.45f, 0.45f, 3), new Vector3(3, 3, 0.01f), new Color(0.1f, 0.4f, 0.7f, 0.3f));
        var material = glass.sharedMaterial;
        material.SetFloat("_Mode", 2);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.renderQueue = 3000;
        renderer.Clear();
        baseline = Render(camera);
        renderer.Record(camera, players, Empty, 2, 2, 0, 64);
        after = Render(camera);
        var center = Point(camera, new Vector3(-0.45f, 0.45f, 5));
        Require(ColorDistance(baseline.GetPixel(center.x, center.y), after.GetPixel(center.x, center.y)) > 0.1f,
            camera.actualRenderingPath + ": translucent fill remains visible behind glass");
        Save(after, "glass-" + camera.actualRenderingPath);
        UnityEngine.Object.DestroyImmediate(glass.gameObject);
        UnityEngine.Object.DestroyImmediate(baseline);
        UnityEngine.Object.DestroyImmediate(after);
    }

    private static MeshRenderer Cube(string name, Vector3 position, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.transform.position = position; go.transform.localScale = scale;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = new Material(Shader.Find("Standard")) { color = color };
        return renderer;
    }

    private static Vector2Int Point(Camera camera, Vector3 point)
    {
        var p = camera.WorldToViewportPoint(point);
        return new Vector2Int(Mathf.Clamp((int)(p.x * Size), 0, Size - 1), Mathf.Clamp((int)(p.y * Size), 0, Size - 1));
    }

    private static Texture2D Render(Camera camera)
    {
        camera.Render();
        return Read(camera.targetTexture);
    }

    private static Texture2D Read(RenderTexture texture)
    {
        var previous = RenderTexture.active;
        RenderTexture.active = texture;
        var image = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        return image;
    }

    private static float ColorDistance(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    private static int ChangedPixels(Texture2D a, Texture2D b)
    {
        var x = a.GetPixels(); var y = b.GetPixels(); int count = 0;
        for (int i = 0; i < x.Length; i++) if (ColorDistance(x[i], y[i]) > 0.025f) count++;
        return count;
    }
    private static void Save(Texture2D image, string name) => File.WriteAllBytes(Path.Combine(TrainerShaderBuild.Output, name + ".png"), image.EncodeToPNG());
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("GRAPHICS CHECK FAILED: " + label);
        _checks++;
        Debug.Log("PASS " + label);
    }
}
