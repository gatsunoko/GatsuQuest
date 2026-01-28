using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// オブジェクトの透明度を制御するクラス (URP Lit Shader対応)
/// ObstacleTransparencyManagerから自動的に追加・制御されます。
/// </summary>
public class ObstacleFader : MonoBehaviour
{
    private Renderer[] renderers;
    private List<Material> materials = new List<Material>();
    private List<float> initialAlphas = new List<float>();

    // 透過時の目標アルファ値
    private float targetAlpha = 0.3f;
    // フェードにかかる時間
    private float fadeSpeed = 5.0f;
    
    private bool isFadingOut = false;
    private Coroutine currentCoroutine;

    // 元のマテリアル設定を保存するための構造体
    private struct MaterialMode
    {
        public float surfaceType;
        public float blendMode;
        public float srcBlend;
        public float dstBlend;
        public float zWrite;
        public int renderQueue;
    }
    
    private List<MaterialMode> originalModes = new List<MaterialMode>();

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            foreach (var m in r.materials)
            {
                materials.Add(m);
                // 元のAlpha値を保存（プロパティが存在しない場合は1.0）
                initialAlphas.Add(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : 1.0f);
                
                // URP Lit Shaderの設定を保存
                MaterialMode mode = new MaterialMode();
                if (m.HasProperty("_Surface")) mode.surfaceType = m.GetFloat("_Surface");
                if (m.HasProperty("_Blend")) mode.blendMode = m.GetFloat("_Blend");
                if (m.HasProperty("_SrcBlend")) mode.srcBlend = m.GetFloat("_SrcBlend");
                if (m.HasProperty("_DstBlend")) mode.dstBlend = m.GetFloat("_DstBlend");
                if (m.HasProperty("_ZWrite")) mode.zWrite = m.GetFloat("_ZWrite");
                mode.renderQueue = m.renderQueue;
                
                originalModes.Add(mode);
            }
        }
    }

    /// <summary>
    /// フェードアウト（透明化）を開始
    /// </summary>
    public void FadeOut(float targetAlphaValue = 0.3f, float speed = 5.0f)
    {
        targetAlpha = targetAlphaValue;
        fadeSpeed = speed;
        isFadingOut = true;

        // まだ透明モードでないなら切り替える
        SetMaterialTransparent();

        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    /// <summary>
    /// フェードイン（不透明化）を開始
    /// </summary>
    public void FadeIn(float speed = 5.0f)
    {
        fadeSpeed = speed;
        isFadingOut = false;

        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(FadeRoutine(1.0f));
    }

    /// <summary>
    /// フェード処理のコルーチン
    /// </summary>
    private IEnumerator FadeRoutine(float goalAlpha)
    {
        bool stillFading = true;
        while (stillFading)
        {
            stillFading = false;
            for (int i = 0; i < materials.Count; i++)
            {
                Material m = materials[i];
                if (!m.HasProperty("_BaseColor")) continue;

                Color color = m.GetColor("_BaseColor");
                float currentAlpha = color.a;
                
                // 目標値に向かって補間
                float newAlpha = Mathf.MoveTowards(currentAlpha, goalAlpha, Time.deltaTime * fadeSpeed);
                
                color.a = newAlpha;
                m.SetColor("_BaseColor", color);

                if (Mathf.Abs(newAlpha - goalAlpha) > 0.01f)
                {
                    stillFading = true;
                }
            }
            yield return null;
        }

        // 完全に不透明に戻ったら、マテリアル設定を元（Opaque）に戻す
        if (!isFadingOut && goalAlpha >= 0.99f)
        {
            RestoreMaterialOpaque();
        }
    }

    /// <summary>
    /// マテリアルを透明モード（Transparent）に切り替え
    /// </summary>
    private void SetMaterialTransparent()
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            
            // URP Lit Shaderのプロパティを変更して透過可能にする
            // Surface Type: Transparent (1)
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1.0f);
            
            // Blending Mode: Alpha (0) - 一般的な透過
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0.0f);

            // ZWrite: Off (0) - 透過オブジェクトは深度バッファに書き込まないのが一般的だが、
            // 壁の場合は書き込んだほうが前後関係が自然な場合もある。一旦Offにする。
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0.0f);
            
            // 標準的な透過ブレンド設定
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            
            // シェーダーキーワードの有効化（これをしないと反映されない場合がある）
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); 
        }
    }

    /// <summary>
    /// マテリアルを元の設定（通常はOpaque）に戻す
    /// </summary>
    private void RestoreMaterialOpaque()
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Material m = materials[i];
            MaterialMode original = originalModes[i];

            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", original.surfaceType);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", original.blendMode);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", original.srcBlend);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", original.dstBlend);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", original.zWrite);
            
            m.renderQueue = original.renderQueue;

            // キーワードの復元
            if (original.surfaceType == 0) // Opaque
            {
                m.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else // Transparent
            {
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_SURFACE_TYPE_OPAQUE");
            }
             m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }
}
