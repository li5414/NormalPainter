using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class NormalEditor : MonoBehaviour
{
#if UNITY_EDITOR


    int GetMouseVertex(Event e, bool allowBackface = false)
    {
        //if (Tools.current != Tool.None)
        //{
        //    Debug.Log(Tools.current);
        //    return -1;
        //}

        Ray mouseRay = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        float minDistance = float.MaxValue;
        int found = -1;
        Quaternion rotation = GetComponent<Transform>().rotation;
        for (int i = 0; i < m_points.Length; i++)
        {
            Vector3 dir = m_points[i] - mouseRay.origin;
            float sqrDistance = Vector3.Cross(dir, mouseRay.direction).sqrMagnitude;
            bool forwardFacing = Vector3.Dot(rotation * m_normals[i], Camera.current.transform.forward) <= 0;
            if ((forwardFacing || allowBackface) && sqrDistance < minDistance && sqrDistance < 0.05f * 0.05f)
            {
                minDistance = sqrDistance;
                found = i;
            }
        }
        return found;
    }


    public void SetClipboard(Vector3 v)
    {
        m_clipboard = v;
    }
    public void ApplyPaste()
    {
        if (m_numSelected > 0)
        {
            for (int i = 0; i < m_points.Length; i++)
            {
                float s = m_selection[i];
                if (s > 0.0f)
                {
                    m_normals[i] = Vector3.Lerp(m_normals[i], m_clipboard, s).normalized;
                }
            }
            ApplyMirroring();
            UpdateNormals();
        }
    }

    public void ApplyMove(Vector3 move)
    {
        for (int i = 0; i < m_selection.Length; ++i)
        {
            float s = m_selection[i];
            if (s > 0.0f)
            {
                m_normals[i] = (m_normals[i] + move * s).normalized;
            }
        }
        ApplyMirroring();
        UpdateNormals();
    }

    public void ApplyRotation(Quaternion rot, Vector3 pivot)
    {
        for (int i = 0; i < m_selection.Length; ++i)
        {
            float s = m_selection[i];
            if (s > 0.0f)
            {
                m_normals[i] = Vector3.Lerp(m_normals[i], rot * m_normals[i], s).normalized;
            }
        }
        ApplyMirroring();
        UpdateNormals();
    }

    public void ApplyScale(Vector3 size, Vector3 pivot, Quaternion rot)
    {
        for (int i = 0; i < m_selection.Length; ++i)
        {
            float s = m_selection[i];
            if (s > 0.0f)
            {
                var dir = (m_points[i] - pivot).normalized;
                dir.x *= size.x;
                dir.y *= size.y;
                dir.z *= size.z;
                m_normals[i] = (m_normals[i] + dir * s).normalized;
            }
        }
        ApplyMirroring();
        UpdateNormals();
    }

    public void ApplyEqualize(float strength)
    {

    }

    public bool ApplyEqualizeBrush(Ray ray, float radius, float pow, float strength)
    {
        Matrix4x4 trans = GetComponent<Transform>().localToWorldMatrix;
        if (neEqualizeRaycast(ray.origin, ray.direction,
            m_points, m_triangles, m_points.Length, m_triangles.Length / 3, radius, pow, strength, m_normals, ref trans) > 0)
        {
            ApplyMirroring();
            UpdateNormals();
            return true;
        }
        return false;
    }

    public void PushUndo()
    {
        Undo.RecordObject(this, "NormalEditor");
        m_history.count++;
        m_history.normals = (Vector3[])m_normals.Clone();
    }

    public void OnUndoRedo()
    {
        if(m_history.normals.Length > 0)
        {
            m_normals = m_history.normals;
            UpdateNormals();
        }
    }

    public void UpdateNormals(bool upload = true)
    {
        if (m_cbNormals != null)
            m_cbNormals.SetData(m_normals);

        if (m_meshTarget != null)
        {
            m_meshTarget.normals = m_normals;
            if (upload)
                m_meshTarget.UploadMeshData(false);
        }
    }

    public void UpdateSelection()
    {
        int prevSelected = m_numSelected;

        float st = 0.0f;
        m_numSelected = 0;
        m_selectionPos = Vector3.zero;
        m_selectionNormal = Vector3.zero;
        int numPoints = m_points.Length;

        for (int i = 0; i < numPoints; ++i)
        {
            float s = m_selection[i];
            if (s > 0.0f)
            {
                m_selectionPos += m_points[i] * s;
                m_selectionNormal += m_normals[i] * s;
                ++m_numSelected;
                st += s;
            }
        }

        if (m_numSelected > 0)
        {
            m_selectionPos /= st;
            m_selectionNormal /= st;
            m_selectionNormal = m_selectionNormal.normalized;
            m_selectionRot = Quaternion.LookRotation(m_selectionNormal);

            var trans = GetComponent<Transform>();
            m_pivotPos = m_selectionPos + trans.position;
            m_pivotRot = m_selectionRot;
        }

        if(prevSelected == 0 && m_numSelected == 0)
        {
            // no need to upload
        }
        else
        {
            m_cbSelection.SetData(m_selection);
        }
    }

    public void ResetNormals()
    {
        m_meshTarget.RecalculateNormals();
        m_normals = m_meshTarget.normals;
        UpdateNormals();
        PushUndo();
    }

    public void RecalculateTangents()
    {
        m_meshTarget.RecalculateTangents();
        m_tangents = m_meshTarget.tangents;
        if(m_cbTangents == null)
            m_cbTangents = new ComputeBuffer(m_tangents.Length, 16);
        m_cbTangents.SetData(m_tangents);
    }


    public bool Raycast(Ray ray, ref int ti, ref float distance)
    {
        Matrix4x4 trans = GetComponent<Transform>().localToWorldMatrix;
        bool ret = neRaycast(ray.origin, ray.direction,
            m_points, m_triangles, m_triangles.Length / 3, ref ti, ref distance, ref trans) > 0;
        return ret;
    }


    public bool SelectAll()
    {
        for (int i = 0; i < m_selection.Length; ++i)
            m_selection[i] = 1.0f;
        return m_selection.Length > 0;
    }

    public bool SelectNone()
    {
        System.Array.Clear(m_selection, 0, m_selection.Length);
        return m_selection.Length > 0;
    }

    public bool SelectSoft(Ray ray, float radius, float pow, float strength)
    {
        Matrix4x4 trans = GetComponent<Transform>().localToWorldMatrix;
        bool ret = neSoftSelection(ray.origin, ray.direction,
            m_points, m_triangles, m_points.Length, m_triangles.Length/3, radius, pow, strength, m_selection, ref trans) > 0;
        return ret;
    }

    public bool SelectHard(Ray ray, float radius, float strength)
    {
        Matrix4x4 trans = GetComponent<Transform>().localToWorldMatrix;
        bool ret = neHardSelection(ray.origin, ray.direction,
            m_points, m_triangles, m_points.Length, m_triangles.Length / 3, radius, strength, m_selection, ref trans) > 0;
        return ret;
    }

    public bool SelectRect(Vector2 r1, Vector2 r2)
    {
        var cam = SceneView.lastActiveSceneView.camera;
        if (cam == null) { return false; }

        var mvp = cam.projectionMatrix * cam.worldToCameraMatrix * GetComponent<Transform>().localToWorldMatrix;
        r1.x = r1.x / cam.pixelWidth * 2.0f - 1.0f;
        r2.x = r2.x / cam.pixelWidth * 2.0f - 1.0f;
        r1.y = (1.0f - r1.y / cam.pixelHeight) * 2.0f - 1.0f;
        r2.y = (1.0f - r2.y / cam.pixelHeight) * 2.0f - 1.0f;
        bool ret = neRectSelection(m_points, m_points.Length, m_selection, ref mvp,
            new Vector2(Math.Min(r1.x, r2.x), Math.Min(r1.y, r2.y)),
            new Vector2(Math.Max(r1.x, r2.x), Math.Max(r1.y, r2.y))) > 0;
        return ret;
    }


    public void ApplyMirroring()
    {
        if (m_mirrorMode == MirrorMode.None) return;

        Vector3 planeNormal = Vector3.up;
        switch (m_mirrorMode)
        {
            case MirrorMode.RightToLeft:
                planeNormal = Vector3.left;
                break;
            case MirrorMode.LeftToRight:
                planeNormal = Vector3.right;
                break;
            case MirrorMode.ForwardToBack:
                planeNormal = Vector3.back;
                break;
            case MirrorMode.BackToForward:
                planeNormal = Vector3.forward;
                break;
            case MirrorMode.UpToDown:
                planeNormal = Vector3.down;
                break;
            case MirrorMode.DownToUp:
                planeNormal = Vector3.up;
                break;
        }

        if (m_mirrorRelation == null)
        {
            m_mirrorRelation = new int[m_normals.Length];
            if (neBuildMirroringRelation(m_points, m_points.Length, planeNormal, 0.001f, m_mirrorRelation) == 0)
            {
                Debug.LogWarning("NormalEditor: this mesh seems not symmetrical");
                m_mirrorRelation = null;
                m_mirrorMode = MirrorMode.None;
                return;
            }
        }
        neApplyMirroring(m_mirrorRelation, m_normals.Length, planeNormal, m_normals);
    }


    static Rect FromToRect(Vector2 start, Vector2 end)
    {
        Rect r = new Rect(start.x, start.y, end.x - start.x, end.y - start.y);
        if (r.width < 0)
        {
            r.x += r.width;
            r.width = -r.width;
        }
        if (r.height < 0)
        {
            r.y += r.height;
            r.height = -r.height;
        }
        return r;
    }

    public void ResetDisplayOptions()
    {
        m_vertexSize = 0.0075f;
        m_normalSize = 0.10f;
        m_tangentSize = 0.075f;
        m_binormalSize = 0.06f;
        m_vertexColor = new Color(0.15f, 0.15f, 0.4f, 0.75f);
        m_vertexColor2 = new Color(1.0f, 0.0f, 0.0f, 0.75f);
        m_normalColor = Color.yellow;
        m_tangentColor = Color.cyan;
        m_binormalColor = Color.green;
    }

    public bool BakeToTexture(int width, int height, string path)
    {
        if (path == null || path.Length == 0)
            return false;

        m_matBake.SetBuffer("_BaseNormals", m_cbBaseNormals);
        m_matBake.SetBuffer("_BaseTangents", m_cbBaseTangents);

        var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf);
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAHalf, false);
        rt.Create();

        m_cmdDraw.Clear();
        m_cmdDraw.SetRenderTarget(rt);
        for (int si = 0; si < m_meshTarget.subMeshCount; ++si)
            m_cmdDraw.DrawMesh(m_meshTarget, Matrix4x4.identity, m_matBake, si, 1);
        Graphics.ExecuteCommandBuffer(m_cmdDraw);

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0, false);
        tex.Apply();
        RenderTexture.active = null;

        if (path.EndsWith(".png"))
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        else
            System.IO.File.WriteAllBytes(path, tex.EncodeToEXR());


        DestroyImmediate(tex);
        DestroyImmediate(rt);

        return true;
    }

    public bool BakeFromTexture(Texture tex)
    {
        if (tex == null)
            return false;

        bool packed = false;
        {
            var path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
                packed = importer.textureType == TextureImporterType.NormalMap;
        }

        var cbUV = new ComputeBuffer(m_normals.Length, 8);
        cbUV.SetData(m_meshTarget.uv);

        m_csBakeFromMap.SetInt("_Packed", packed ? 1 : 0);
        m_csBakeFromMap.SetTexture(0, "_NormalMap", tex);
        m_csBakeFromMap.SetBuffer(0, "_UV", cbUV);
        m_csBakeFromMap.SetBuffer(0, "_Normals", m_cbBaseNormals);
        m_csBakeFromMap.SetBuffer(0, "_Tangents", m_cbBaseTangents);
        m_csBakeFromMap.SetBuffer(0, "_Dst", m_cbNormals);
        m_csBakeFromMap.Dispatch(0, m_normals.Length, 1, 1);

        m_cbNormals.GetData(m_normals);
        cbUV.Dispose();

        UpdateNormals();
        PushUndo();

        return true;
    }


    [DllImport("MeshSyncServer")] static extern int neRaycast(
        Vector3 pos, Vector3 dir, Vector3[] vertices, int[] indices, int num_triangles,
        ref int tindex, ref float distance, ref Matrix4x4 trans);

    [DllImport("MeshSyncServer")] static extern int neSoftSelection(
        Vector3 pos, Vector3 dir, Vector3[] vertices, int[] indices, int num_vertices, int num_triangles,
        float radius, float strength, float pow, float[] seletion, ref Matrix4x4 trans);
    
    [DllImport("MeshSyncServer")] static extern int neHardSelection(
        Vector3 pos, Vector3 dir, Vector3[] vertices, int[] indices, int num_vertices, int num_triangles,
        float radius, float strength, float[] seletion, ref Matrix4x4 trans);

    [DllImport("MeshSyncServer")] static extern int neRectSelection(
        Vector3[] vertices, int num_vertices, float[] seletion,
        ref Matrix4x4 mvp, Vector2 rmin, Vector2 rmax);

    [DllImport("MeshSyncServer")] static extern int neEqualize(
        int num_vertices, int num_triangles,
        float radius, float strength, float pow, Vector3[] normals, ref Matrix4x4 trans);

    [DllImport("MeshSyncServer")] static extern int neEqualizeRaycast(
        Vector3 pos, Vector3 dir, Vector3[] vertices, int[] indices, int num_vertices, int num_triangles,
        float radius, float strength, float pow, Vector3[] normals, ref Matrix4x4 trans);

    [DllImport("MeshSyncServer")] static extern int neBuildMirroringRelation(
        Vector3[] vertices, int num_vertices, Vector3 plane_normal, float epsilon, int[] relation);

    [DllImport("MeshSyncServer")] static extern void neApplyMirroring(
        int[] relation, int num_vertices, Vector3 plane_normal, Vector3[] normals);

#endif
}