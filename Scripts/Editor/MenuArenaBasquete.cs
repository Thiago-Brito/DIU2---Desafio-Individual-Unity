#if UNITY_EDITOR
using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MenuArenaBasquete
{
    [InitializeOnLoadMethod]
    private static void CorrigirGameViewsAbertas()
    {
        EditorApplication.playModeStateChanged -= AntesDoPlay;
        EditorApplication.playModeStateChanged += AntesDoPlay;
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) RepararAssets();
            foreach (var janela in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (janela.GetType().FullName == "UnityEditor.GameView") ConfigurarNitidez(janela);
        };
    }

    private static void AntesDoPlay(PlayModeStateChange estado)
    {
        if (estado == PlayModeStateChange.ExitingEditMode) RepararAssets();
    }

    [MenuItem("Jogo 2D/Reparar referencias da arena")]
    public static void RepararAssets()
    {
        foreach (var arena in Resources.FindObjectsOfTypeAll<ArenaBasquete>())
        {
            if (!arena.gameObject.scene.IsValid() || !arena.gameObject.scene.isLoaded) continue;
            if (!arena.RestaurarAssetsNoEditor()) continue;
            EditorUtility.SetDirty(arena);
            if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
        }
    }

    // Corrige o preview, sem alterar os sprites ou a resolucao do monitor.
    public static bool ConfigurarNitidez(EditorWindow janela)
    {
        var dados = new SerializedObject(janela);
        var baixaResolucao = dados.FindProperty("m_LowResolutionForAspectRatios");
        if (baixaResolucao != null && baixaResolucao.isArray)
            for (int i = 0; i < baixaResolucao.arraySize; i++)
                baixaResolucao.GetArrayElementAtIndex(i).boolValue = false;
        var zoom = dados.FindProperty("m_ZoomArea")?.FindPropertyRelative("m_Scale");
        if (zoom != null) zoom.vector2Value = Vector2.one;
        dados.ApplyModifiedPropertiesWithoutUndo();
        janela.Repaint();
        return baixaResolucao != null && zoom != null;
    }

    [MenuItem("Jogo 2D/Corrigir nitidez da janela Game")]
    public static void CorrigirNitidez()
    {
        var tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (tipo == null) return;
        var janela = EditorWindow.GetWindow(tipo);
        janela.Focus();
        if (ConfigurarNitidez(janela)) Debug.Log("Game: baixa resolucao desativada e escala em 100%.");
        else Debug.LogWarning("Confira na aba Game: desative Low Resolution Aspect Ratios e use Scale 1x.");
    }

    [MenuItem("Jogo 2D/Maximizar ou restaurar janela Game")]
    public static void MaximizarGame()
    {
        var tipo = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (tipo == null) return;
        var janela = EditorWindow.GetWindow(tipo);
        janela.Focus(); janela.maximized = !janela.maximized;
        ConfigurarNitidez(janela);
    }

    [MenuItem("Jogo 2D/Abrir Quadra Lunar (boss)")]
    public static void Abrir()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Saia do Play para abrir a arena."); return; }
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/Scenes/ArenaBasquete.unity");
    }

    [MenuItem("Jogo 2D/Exportar Quadra Lunar para WebGL")]
    public static void ExportarWebGL()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Saia do Play antes de exportar o jogo.");
            return;
        }
        RepararAssets();
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            Debug.LogError("Instale WebGL Build Support pelo Unity Hub para esta versao da Unity e execute novamente.");
            EditorUtility.DisplayDialog("Falta o suporte WebGL", "No Unity Hub: Installs > Unity " + Application.unityVersion + " > Add modules > Web Build Support. Depois reinicie a Unity e use este comando novamente.", "Entendi");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        PlayerSettings.productName = "Quadra Lunar";
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.template = "APPLICATION:Default";
        AssetDatabase.SaveAssets();
        // Cada exportacao usa uma pasta nova para nao incluir arquivos de builds antigos.
        string destino = "Builds/WebGL/" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
        var relatorio = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/ArenaBasquete.unity" },
            locationPathName = destino, target = BuildTarget.WebGL, options = BuildOptions.None
        });
        if (relatorio.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Build WebGL falhou. Nenhum ZIP novo foi gerado; consulte o Console.");
            return;
        }
        try
        {
            string zip = Path.GetFullPath(destino + ".zip");
            EmpacotarWebGL(destino, zip);
            Debug.Log("Pronto para enviar ao itch.io: " + zip);
            EditorUtility.RevealInFinder(zip);
            EditorUtility.DisplayDialog("ZIP pronto para publicar", "Envie este ZIP para um projeto HTML no itch.io e marque 'This file will be played in the browser'.\n\n" + zip + "\n\nPasso a passo e texto da pagina: Docs/PUBLICAR-WEBGL.md", "Entendi");
        }
        catch (Exception erro)
        {
            Debug.LogError("O jogo foi exportado, mas o ZIP falhou: " + erro.Message + "\nArquivos em " + destino);
        }
    }

    public static void EmpacotarWebGL(string pasta, string destinoZip)
    {
        string origem = Path.GetFullPath(pasta);
        if (!File.Exists(Path.Combine(origem, "index.html")) || !Directory.Exists(Path.Combine(origem, "Build")))
            throw new InvalidOperationException("Build incompleto: faltam index.html ou a pasta Build.");
        // Inclui somente os arquivos distribuidos ao navegador, sem simbolos de depuracao.
        using (var arquivo = new FileStream(destinoZip, FileMode.CreateNew))
        using (var zip = new ZipArchive(arquivo, ZipArchiveMode.Create))
        {
            foreach (string caminho in Directory.GetFiles(origem, "*", SearchOption.AllDirectories))
            {
                string relativo = caminho.Substring(origem.Length + 1).Replace('\\', '/');
                if (relativo.IndexOf("_DoNotShip", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var entrada = zip.CreateEntry(relativo, System.IO.Compression.CompressionLevel.Optimal);
                using (var leitura = File.OpenRead(caminho))
                using (var escrita = entrada.Open()) leitura.CopyTo(escrita);
            }
        }
    }
}

// Tambem cobre o build feito pela janela padrao da Unity, inclusive de cenas fechadas.
public class PrepararAssetsArenaNoBuild : UnityEditor.Build.IProcessSceneWithReport
{
    public int callbackOrder => 0;
    public void OnProcessScene(UnityEngine.SceneManagement.Scene cena, BuildReport relatorio)
    {
        foreach (var raiz in cena.GetRootGameObjects())
            foreach (var arena in raiz.GetComponentsInChildren<ArenaBasquete>(true))
            {
                arena.RestaurarAssetsNoEditor();
                string faltantes = arena.AssetsFaltantes();
                if (faltantes.Length > 0) throw new UnityEditor.Build.BuildFailedException("Arena sem referencias: " + faltantes);
            }
    }
}
#endif
