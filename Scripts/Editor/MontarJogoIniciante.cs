#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MontarJogoIniciante
{
    private const string PastaPersonagem = "Assets/PNG/Adventurer/Poses/";

    static MontarJogoIniciante()
    {
        EditorApplication.delayCall += GarantirQueOsBlocosExistem;
    }

    private static void GarantirQueOsBlocosExistem()
    {
        if (Application.isPlaying || EditorApplication.isCompiling) return;
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity") return;

        ConfigurarSpritesSemBorrar();

        // Se a fase existe mas não tem nenhum Tile visual, ela ficou invisível.
        // Nesse caso o script reconstrói a cena sem depender de EditorPrefs ou menu.
        GameObject aventureiro = GameObject.Find("FASE INICIANTE/Aventureiro");
        CapsuleCollider2D colliderAtual = aventureiro != null ? aventureiro.GetComponent<CapsuleCollider2D>() : null;
        PlayerController2D controleAtual = aventureiro != null ? aventureiro.GetComponent<PlayerController2D>() : null;
        bool colliderAntigo = colliderAtual != null &&
                              (colliderAtual.size.y > 1f || Mathf.Abs(colliderAtual.offset.y - 0.10f) > 0.01f);
        bool faltaAnimacaoDaBola = controleAtual == null ||
            new SerializedObject(controleAtual).FindProperty("conduzindo1").objectReferenceValue == null;

        bool faltaBola = GameObject.Find("FASE INICIANTE/Aventureiro/Bola de basquete") == null;

        if (GameObject.Find("FASE INICIANTE/Chao principal/Tile 1") == null || colliderAntigo || faltaBola || faltaAnimacaoDaBola)
            MontarCena();
        else if (Object.FindFirstObjectByType<PartidaAlien>() == null)
        {
            ConfigurarCombate(aventureiro.transform.parent, controleAtual);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
    }

    [MenuItem("Jogo 2D/Montar ou refazer fase inicial")]
    public static void MontarCena()
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid()) return;

        ConfigurarSpritesSemBorrar();

        GameObject antigo = GameObject.Find("FASE INICIANTE");
        if (antigo != null) Object.DestroyImmediate(antigo);

        GameObject raiz = new GameObject("FASE INICIANTE");
        CriarFundo(raiz.transform);

        GameObject jogador = new GameObject("Aventureiro");
        jogador.transform.SetParent(raiz.transform);
        jogador.transform.position = new Vector3(-6f, -1.7f, 0f);
        SpriteRenderer render = jogador.AddComponent<SpriteRenderer>();
        render.sprite = Sprite("adventurer_idle.png");
        render.sortingOrder = 5;

        Rigidbody2D corpo = jogador.AddComponent<Rigidbody2D>();
        corpo.freezeRotation = true;
        corpo.gravityScale = 3f;
        corpo.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CapsuleCollider2D colisor = jogador.AddComponent<CapsuleCollider2D>();
        // O sprite mede perto de 0.93 unidade. Um collider de 1.65 fazia o
        // desenho parar no ar, mesmo com a colisão encostada corretamente.
        colisor.size = new Vector2(0.56f, 0.92f);
        // A imagem PNG tem uma margem transparente abaixo dos pés. Subir o
        // collider faz o desenho descer até encostar visualmente no chão.
        colisor.offset = new Vector2(0f, 0.10f);

        PlayerController2D controle = jogador.AddComponent<PlayerController2D>();
        SerializedObject dados = new SerializedObject(controle);
        dados.FindProperty("parado").objectReferenceValue = Sprite("adventurer_idle.png");
        dados.FindProperty("caminhada1").objectReferenceValue = Sprite("adventurer_walk1.png");
        dados.FindProperty("caminhada2").objectReferenceValue = Sprite("adventurer_walk2.png");
        dados.FindProperty("pulando").objectReferenceValue = Sprite("adventurer_jump.png");
        dados.FindProperty("abaixado").objectReferenceValue = Sprite("adventurer_duck.png");
        dados.FindProperty("deslizando").objectReferenceValue = Sprite("adventurer_slide.png");
        dados.FindProperty("conduzindo1").objectReferenceValue = Sprite("adventurer_action1.png");
        dados.FindProperty("conduzindo2").objectReferenceValue = Sprite("adventurer_action2.png");
        dados.FindProperty("segurandoBola").objectReferenceValue = Sprite("adventurer_hold1.png");
        dados.ApplyModifiedPropertiesWithoutUndo();

        CriarBola(jogador.transform, controle);
        ConfigurarCombate(raiz.transform, controle);

        CriarPlataforma(raiz.transform, "Chao principal", new Vector2(0f, -3.3f), new Vector2(18f, 1f), "block_green.svg");
        CriarPlataforma(raiz.transform, "Plataforma 1", new Vector2(4.5f, -1.35f), new Vector2(3f, 0.55f), "block_planks.svg");
        CriarPlataforma(raiz.transform, "Plataforma 2", new Vector2(9f, 0.15f), new Vector2(3.4f, 0.55f), "block_planks.svg");
        CriarPlataforma(raiz.transform, "Plataforma 3", new Vector2(14f, -1.1f), new Vector2(4f, 0.55f), "block_planks.svg");
        CriarPlataforma(raiz.transform, "Chao final", new Vector2(20f, -3.3f), new Vector2(12f, 1f), "block_green.svg");

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.38f, 0.72f, 0.94f);
            CameraSeguir seguir = camera.GetComponent<CameraSeguir>() ?? camera.gameObject.AddComponent<CameraSeguir>();
            seguir.Configurar(jogador.transform);
            camera.transform.position = new Vector3(0f, 1.5f, -10f);
        }

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Selection.activeGameObject = jogador;
        Debug.Log("Fase pronta! Aperte Play. Setas/A-D andam, Espaço pula, S abaixa e Shift desliza.");
    }

    private static Sprite Sprite(string arquivo) => AssetDatabase.LoadAssetAtPath<Sprite>(PastaPersonagem + arquivo);

    private static void ConfigurarSpritesSemBorrar()
    {
        string[] caminhos = AssetDatabase.FindAssets("t:Texture2D", new[]
        {
            "Assets/PNG/Adventurer/Poses",
            "Assets"
        });

        foreach (string guid in caminhos)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            bool personagem = caminho.StartsWith("Assets/PNG/Adventurer/Poses/");
            bool bola = caminho.StartsWith("Assets/ball_basket");
            bool alien = caminho.StartsWith("Assets/alienBlue_");
            if (!personagem && !bola && !alien) continue;

            TextureImporter importer = AssetImporter.GetAtPath(caminho) as TextureImporter;
            if (importer == null) continue;
            if (importer.filterMode == FilterMode.Point &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                !importer.mipmapEnabled && importer.textureType == TextureImporterType.Sprite &&
                (!alien || importer.spriteImportMode == SpriteImportMode.Single)) continue;

            importer.textureType = TextureImporterType.Sprite;
            if (alien)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
            }
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static void CriarBola(Transform pai, PlayerController2D controle)
    {
        GameObject bola = new GameObject("Bola de basquete");
        bola.transform.SetParent(pai);
        bola.transform.localPosition = new Vector3(0.34f, 0.08f, -0.1f);

        Sprite[] quadros = new Sprite[4];
        for (int i = 0; i < quadros.Length; i++)
        {
            Object[] objetos = AssetDatabase.LoadAllAssetsAtPath("Assets/ball_basket" + (i + 1) + ".png");
            foreach (Object objeto in objetos)
            {
                if (objeto is Sprite sprite)
                {
                    quadros[i] = sprite;
                    break;
                }
            }
        }

        SpriteRenderer render = bola.AddComponent<SpriteRenderer>();
        render.sprite = quadros[0];
        render.sortingOrder = 6;

        BolaBasquete movimento = bola.AddComponent<BolaBasquete>();
        SerializedObject dados = new SerializedObject(movimento);
        dados.FindProperty("jogador").objectReferenceValue = controle;
        SerializedProperty imagens = dados.FindProperty("imagensDaBola");
        imagens.arraySize = quadros.Length;
        for (int i = 0; i < quadros.Length; i++)
            imagens.GetArrayElementAtIndex(i).objectReferenceValue = quadros[i];
        dados.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurarCombate(Transform raiz, PlayerController2D jogador)
    {
        GameObject objeto = new GameObject("Partida Alien Blue");
        objeto.transform.SetParent(raiz);
        PartidaAlien partida = objeto.AddComponent<PartidaAlien>();
        SerializedObject dados = new SerializedObject(partida);
        dados.FindProperty("jogador").objectReferenceValue = jogador;
        SerializedProperty aliens = dados.FindProperty("alienQuadros");
        aliens.arraySize = 2;
        for (int i = 0; i < 2; i++)
            aliens.GetArrayElementAtIndex(i).objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/alienBlue_walk" + (i + 1) + ".png");
        SerializedProperty bolas = dados.FindProperty("bolaQuadros");
        bolas.arraySize = 4;
        for (int i = 0; i < 4; i++)
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/ball_basket" + (i + 1) + ".png"))
                if (asset is Sprite sprite) { bolas.GetArrayElementAtIndex(i).objectReferenceValue = sprite; break; }
        dados.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CriarFundo(Transform pai)
    {
        GameObject fundo = new GameObject("Ceu");
        fundo.transform.SetParent(pai);
        fundo.transform.position = new Vector3(8f, 1f, 5f);
        SpriteRenderer sr = fundo.AddComponent<SpriteRenderer>();
        sr.sprite = QuadradoBranco();
        sr.color = new Color(0.38f, 0.72f, 0.94f);
        sr.size = new Vector2(50f, 14f);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = -10;
    }

    private static void CriarPlataforma(Transform pai, string nome, Vector2 posicao, Vector2 tamanho, string arquivoDoTile)
    {
        GameObject bloco = new GameObject(nome);
        bloco.transform.SetParent(pai);
        bloco.transform.position = posicao;
        BoxCollider2D colisao = bloco.AddComponent<BoxCollider2D>();
        colisao.size = tamanho;

        // Usamos um quadrado garantido aqui. Os SVGs deste pacote foram importados
        // como imagens de UI e podem desaparecer quando usados no SpriteRenderer.
        Sprite tile = QuadradoBranco();

        // Desenha vários bloquinhos, mas usa somente um collider para a plataforma inteira.
        int quantidade = Mathf.CeilToInt(tamanho.x);
        float larguraDeCadaUm = tamanho.x / quantidade;
        for (int i = 0; i < quantidade; i++)
        {
            GameObject visual = new GameObject("Tile " + (i + 1));
            visual.transform.SetParent(bloco.transform);
            visual.transform.localPosition = new Vector3(-tamanho.x * 0.5f + larguraDeCadaUm * (i + 0.5f), 0f, 0f);

            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = tile;
            sr.sortingOrder = 1;

            bool chaoVerde = arquivoDoTile == "block_green.svg";
            sr.color = chaoVerde
                ? (i % 2 == 0 ? new Color(0.20f, 0.62f, 0.22f) : new Color(0.16f, 0.52f, 0.18f))
                : (i % 2 == 0 ? new Color(0.58f, 0.36f, 0.16f) : new Color(0.48f, 0.27f, 0.11f));

            Vector2 medida = tile.bounds.size;
            // Um pequeno espaço deixa claro que o chão é feito de vários blocos.
            visual.transform.localScale = new Vector3((larguraDeCadaUm - 0.04f) / medida.x, (tamanho.y - 0.04f) / medida.y, 1f);
        }
    }

    private static Sprite QuadradoBranco()
    {
        const string caminho = "Assets/GeneratedSquare.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
        if (sprite != null) return sprite;

        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, Color.white);
        textura.Apply();
        System.IO.File.WriteAllBytes(caminho, textura.EncodeToPNG());
        Object.DestroyImmediate(textura);
        AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(caminho);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 1f;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(caminho);
    }
}
#endif
