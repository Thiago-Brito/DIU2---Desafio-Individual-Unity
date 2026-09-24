using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

// Cena independente: os assets sao referencias serializadas, inclusive no WebGL.
public class ArenaBasquete : MonoBehaviour
{
    [SerializeField] private Sprite[] jogadorSprites;
    [SerializeField] private Sprite[] alienSprites;
    [SerializeField] private Sprite bolaSprite;
    [SerializeField] private Sprite projetilInimigoSprite;
    [SerializeField] private Sprite cristalSprite;
    [SerializeField] private Sprite quadrado;
    [SerializeField] private AudioClip[] efeitos;
    public static readonly string[] NomesFases = { "AQUECIMENTO", "CHUVA COSMICA", "PATRULHA DA QUADRA", "PRORROGACAO" };
    public PlayerController2D Jogador { get; private set; }
    public ChefeQuadra Chefe { get; private set; }
    public Sprite[] AlienSprites => alienSprites;
    public Material MaterialEfeito { get; private set; }
    public bool EmJogo => estado == Estado.Jogando;
    public int Vidas { get; private set; } = 5;
    public enum Modo { Facil, Medio, Dificil }
    public Modo Dificuldade { get; private set; } = Modo.Facil;
    public int VidasIniciais => Dificuldade == Modo.Facil ? 5 : Dificuldade == Modo.Medio ? 3 : 1;
    public int ProjeteisExtras => Dificuldade != Modo.Facil ? 2 : 0;
    public bool RecarregaAoMatar => Dificuldade != Modo.Dificil;
    public void SelecionarModo(Modo modo)
    {
        if (estado == Estado.Menu && System.Enum.IsDefined(typeof(Modo), modo)) Dificuldade = modo;
    }
    public bool Invulneravel => Time.time < protegidoAte;
    private float protegidoAte;
    private enum Estado { Menu, Jogando, Pausado, Derrota, Vitoria }
    private Estado estado;
    private BolaUnica bola;
    private Camera cameraArena;
    private AudioSource audioFonte;
    private Transform perigos, decoracao;
    private Tile tile;
    private int pontos, energia, recorde, acertos, coletas;
    private float tempo, avisoAte, tremor, proximoAlien;
    private string aviso;
    private GUIStyle titulo, texto, pequeno, botao;
    private bool estilosProntos;

    private void Awake()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        // A cena aberta pode ser anterior aos campos novos, mesmo com o arquivo atualizado.
        RestaurarAssetsNoEditor();
#endif
        string faltantes = AssetsFaltantes();
        if (faltantes.Length > 0)
        { Debug.LogError("Arena: referencias ausentes: " + faltantes + ". Confira o componente ArenaBasquete.", this); enabled = false; return; }
        MaterialEfeito = new Material(Shader.Find("Sprites/Default"));
        audioFonte = gameObject.AddComponent<AudioSource>(); audioFonte.playOnAwake = false;
        cameraArena = Camera.main;
        if (cameraArena == null)
        {
            var objeto = new GameObject("Camera da arena", typeof(Camera), typeof(AudioListener));
            objeto.tag = "MainCamera"; cameraArena = objeto.GetComponent<Camera>();
        }
        cameraArena.orthographic = true;
        cameraArena.allowMSAA = true;
        cameraArena.backgroundColor = new Color(0.045f, 0.065f, 0.14f);
        cameraArena.clearFlags = CameraClearFlags.SolidColor;
        perigos = new GameObject("Ataques e recompensas").transform;
        decoracao = new GameObject("Quadra lunar").transform;
        CriarCenario(); CriarJogador(); CriarChefe();
        var objetoBola = Visual("Bola unica", bolaSprite, Vector2.zero, Vector2.one, Color.white, 9);
        bola = objetoBola.AddComponent<BolaUnica>(); bola.Configurar(this);
        recorde = PlayerPrefs.GetInt("QuadraLunar.Recorde", 0);
        AlterarEstado(Estado.Menu);
    }

    public string AssetsFaltantes()
    {
        var faltantes = new System.Collections.Generic.List<string>();
        for (int i = 0; i < 9; i++)
            if (jogadorSprites == null || i >= jogadorSprites.Length || jogadorSprites[i] == null) faltantes.Add("jogadorSprites[" + i + "]");
        for (int i = 0; i < 2; i++)
            if (alienSprites == null || i >= alienSprites.Length || alienSprites[i] == null) faltantes.Add("alienSprites[" + i + "]");
        if (bolaSprite == null) faltantes.Add("bolaSprite");
        if (projetilInimigoSprite == null) faltantes.Add("projetilInimigoSprite (fireball.png)");
        if (cristalSprite == null) faltantes.Add("cristalSprite (gem_green.png)");
        if (quadrado == null) faltantes.Add("quadrado");
        return string.Join(", ", faltantes);
    }

#if UNITY_EDITOR
    // Chamado no editor e no processamento do build; nunca depende de AssetDatabase no player.
    public bool RestaurarAssetsNoEditor()
    {
        bool mudou = false;
        string[] poses = { "idle", "walk1", "walk2", "jump", "duck", "slide", "action1", "action2", "hold1" };
        if (jogadorSprites == null || jogadorSprites.Length < poses.Length)
        { System.Array.Resize(ref jogadorSprites, poses.Length); mudou = true; }
        for (int i = 0; i < poses.Length; i++)
            mudou |= CompletarSprite(ref jogadorSprites[i], "Assets/PNG/Adventurer/Poses/adventurer_" + poses[i] + ".png");
        if (alienSprites == null || alienSprites.Length < 2)
        { System.Array.Resize(ref alienSprites, 2); mudou = true; }
        for (int i = 0; i < 2; i++) mudou |= CompletarSprite(ref alienSprites[i], "Assets/alienBlue_walk" + (i + 1) + ".png");
        mudou |= CompletarSprite(ref bolaSprite, "Assets/ball_basket1.png");
        mudou |= CompletarSprite(ref projetilInimigoSprite, "Assets/Sprites/Tiles/Double/fireball.png");
        mudou |= CompletarSprite(ref cristalSprite, "Assets/Sprites/Tiles/Double/gem_green.png");
        mudou |= CompletarSprite(ref quadrado, "Assets/GeneratedSquare.png");
        string[] sons = { "throw", "bump", "coin", "hurt", "gem", "magic" };
        if (efeitos == null || efeitos.Length < sons.Length)
        { System.Array.Resize(ref efeitos, sons.Length); mudou = true; }
        for (int i = 0; i < sons.Length; i++)
        {
            if (efeitos[i] != null) continue;
            efeitos[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sounds/sfx_" + sons[i] + ".ogg");
            mudou |= efeitos[i] != null;
        }
        return mudou;
    }
    private static bool CompletarSprite(ref Sprite destino, string caminho)
    {
        if (destino != null) return false;
        // Importacao Multiple guarda o sprite como subasset; nao carregar somente a textura.
        foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(caminho))
            if (asset is Sprite sprite) { destino = sprite; return true; }
        return false;
    }
#endif

    private GameObject Visual(string nome, Sprite sprite, Vector2 posicao, Vector2 escala, Color cor, int ordem)
    {
        var go = new GameObject(nome); go.transform.position = posicao; go.transform.localScale = escala;
        var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = cor; sr.sortingOrder = ordem;
        return go;
    }
    private GameObject Bloco(string nome, Vector2 pos, Vector2 tamanho, Color cor, int ordem = 0, bool solido = false)
    {
        Vector2 medida = quadrado.bounds.size;
        var go = Visual(nome, quadrado, pos, new Vector2(tamanho.x / medida.x, tamanho.y / medida.y), cor, ordem);
        go.transform.SetParent(decoracao);
        if (solido) go.AddComponent<BoxCollider2D>();
        return go;
    }
    private void CriarCenario()
    {
        Bloco("Painel fundo", new Vector2(0f, 0.5f), new Vector2(24f, 14f), new Color(0.07f, 0.10f, 0.21f), -20);
        for (int fila = 0; fila < 3; fila++)
        {
            Bloco("Arquibancada", new Vector2(0f, fila * 0.8f + 0.2f), new Vector2(19f, 0.12f), new Color(0.13f, 0.19f, 0.32f), -15);
            for (int i = 0; i < 25; i++)
                Bloco("Luz torcida", new Vector2(-9f + i * 0.75f, fila * 0.8f + 0.5f), new Vector2(0.16f, 0.18f),
                    i % 3 == 0 ? new Color(0.3f, 0.65f, 0.75f) : new Color(0.22f, 0.26f, 0.44f), -14);
        }
        Bloco("Faixa neon", new Vector2(0f, -2.82f), new Vector2(19f, 0.09f), new Color(0.2f, 0.95f, 0.8f), 2);
        // Tilemap real: nao e apenas uma fileira de GameObjects.
        var grid = new GameObject("Grid da quadra", typeof(Grid)); grid.transform.SetParent(decoracao);
        var mapa = new GameObject("Tilemap - piso", typeof(Tilemap), typeof(TilemapRenderer), typeof(TilemapCollider2D));
        mapa.transform.SetParent(grid.transform);
        tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = quadrado; tile.colliderType = Tile.ColliderType.Grid;
        tile.transform = Matrix4x4.Scale(new Vector3(1f / quadrado.bounds.size.x, 1f / quadrado.bounds.size.y, 1f));
        var tm = mapa.GetComponent<Tilemap>();
        for (int x = -10; x < 10; x++)
            for (int y = -5; y < -3; y++)
            {
                var celula = new Vector3Int(x, y, 0); tm.SetTile(celula, tile);
                tm.SetTileFlags(celula, TileFlags.None);
                tm.SetColor(celula, x % 2 == 0 ? new Color(0.4f, 0.23f, 0.21f) : new Color(0.49f, 0.29f, 0.23f));
            }
        Bloco("Parede esquerda", new Vector2(-9.5f, 1f), new Vector2(0.5f, 10f), new Color(0.2f, 0.55f, 0.65f), 1, true);
        Bloco("Parede direita", new Vector2(9.5f, 1f), new Vector2(0.5f, 10f), new Color(0.2f, 0.55f, 0.65f), 1, true);
        Bloco("Tabela esquerda", new Vector2(-8.2f, 0f), new Vector2(0.16f, 1.4f), Color.white, -2);
        Bloco("Aro esquerdo", new Vector2(-7.8f, -0.4f), new Vector2(0.8f, 0.12f), new Color(1f, 0.45f, 0.2f), -1);
        Bloco("Tabela direita", new Vector2(8.2f, 0f), new Vector2(0.16f, 1.4f), Color.white, -2);
        Bloco("Aro direito", new Vector2(7.8f, -0.4f), new Vector2(0.8f, 0.12f), new Color(1f, 0.45f, 0.2f), -1);
        // Degraus de 1,25 a 1,4 unidades: alcancaveis com o salto original.
        Plataforma("Esquerda baixa", -6f, -1.65f, 2.4f);
        Plataforma("Centro esquerda", -2.9f, -0.35f, 2.3f);
        Plataforma("Passarela alta", 0.2f, 1f, 2.4f);
        Plataforma("Direita baixa", 2.4f, -1.65f, 2.2f);
        Plataforma("Direita alta", 3.3f, -0.25f, 1.9f);
        Plataforma("Apoio da parede direita", 6.6f, -0.9f, 2.4f);
    }
    private void Plataforma(string nome, float x, float y, float largura)
    {
        var plataforma = Bloco("Plataforma - " + nome, new Vector2(x, y), new Vector2(largura, 0.22f), new Color(0.12f, 0.34f, 0.43f), 1, true);
        plataforma.GetComponent<BoxCollider2D>().usedByEffector = true;
        var apoio = plataforma.AddComponent<PlatformEffector2D>();
        apoio.useOneWay = true; apoio.useOneWayGrouping = true; apoio.surfaceArc = 165f;
        Bloco("Borda - " + nome, new Vector2(x, y + 0.095f), new Vector2(largura, 0.035f), new Color(0.42f, 0.95f, 0.86f), 2);
    }
    private void CriarJogador()
    {
        var go = Visual("Jogador", jogadorSprites[0], new Vector2(-5f, -2.4f), Vector2.one, Color.white, 5);
        var rb = go.AddComponent<Rigidbody2D>(); rb.freezeRotation = true; rb.gravityScale = 3f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        var col = go.AddComponent<CapsuleCollider2D>(); col.size = new Vector2(0.56f, 0.92f); col.offset = new Vector2(0f, 0.1f);
        Jogador = go.AddComponent<PlayerController2D>(); Jogador.ConfigurarSprites(jogadorSprites);
    }
    private void CriarChefe()
    {
        var go = Visual("Capitao Orbita", alienSprites[0], new Vector2(6f, -0.6f), Vector2.one * 1.8f, Color.white, 4);
        Chefe = go.AddComponent<ChefeQuadra>(); Chefe.Configurar(this);
    }
    private void Update()
    {
        var k = Keyboard.current;
        if (k != null && k.f11Key.wasPressedThisFrame && !Application.isEditor)
            Screen.fullScreen = !Screen.fullScreen;
        if (k != null && k.escapeKey.wasPressedThisFrame)
        {
            if (EmJogo) AlterarEstado(Estado.Pausado);
            else if (estado == Estado.Pausado) AlterarEstado(Estado.Jogando);
        }
        if (!EmJogo)
        {
            if (k != null && k.enterKey.wasPressedThisFrame && estado != Estado.Pausado) Iniciar();
            return;
        }
        tempo += Time.deltaTime;
        if (Jogador.transform.position.y < -5f)
        {
            Machucar();
            if (EmJogo) Jogador.Reiniciar(new Vector3(-5f, -2.4f, 0f));
            return;
        }
        if (tempo >= proximoAlien)
        {
            if (perigos.GetComponentsInChildren<InimigoQuadra>().Length < 2)
            {
                var alien = Visual("Alien reserva", alienSprites[0], new Vector2(8f, 1f), Vector2.one * 0.65f, Color.white, 5);
                alien.transform.SetParent(perigos); alien.AddComponent<InimigoQuadra>().Configurar(this, 0.75f + Chefe.Fase * 0.18f);
            }
            proximoAlien = tempo + Mathf.Max(5f, 11f - Chefe.Fase * 1.5f);
        }
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool normal = k != null && (k.jKey.wasPressedThisFrame || k.ctrlKey.wasPressedThisFrame);
        bool especial = k != null && k.eKey.wasPressedThisFrame;
        if (bola.NaMao && (mouse || normal || especial))
        {
            if (especial && energia < 40) { Avisar("Especial precisa de 40 de energia", 1.4f); return; }
            Vector2 direcao = bola.DirecaoMira();
            if (especial) energia -= 40;
            bola.Arremessar(direcao, especial);
        }
        else if (!bola.NaMao && (normal || especial)) Avisar("Recupere a bola ou aguarde o retorno", 1f);
    }
    private void LateUpdate()
    {
        if (cameraArena == null || Jogador == null) return;
        Jogador.GetComponent<SpriteRenderer>().color = Invulneravel && EmJogo
            ? new Color(1f, 0.65f, 0.65f, Mathf.PingPong(Time.time * 8f, 0.6f) + 0.4f) : Color.white;
        float minimo = Mathf.Max(5.6f, 10.4f / cameraArena.aspect);
        cameraArena.orthographicSize = Mathf.Lerp(cameraArena.orthographicSize, minimo + (Chefe.Fase == 4 ? 0.2f : 0f), 0.05f);
        Vector3 centro = new Vector3(Jogador.transform.position.x * 0.025f, 0.6f, -10f);
        if (EmJogo && tremor > 0f)
        {
            tremor -= Time.deltaTime;
            centro += new Vector3(Mathf.Sin(Time.time * 75f), Mathf.Cos(Time.time * 63f), 0f) * tremor * 0.25f;
        }
        cameraArena.transform.position = centro;
    }
    private void AlterarEstado(Estado novo)
    {
        estado = novo; Time.timeScale = EmJogo ? 1f : 0f;
        Jogador.ControleBloqueado = !EmJogo;
    }
    public void Iniciar()
    {
        Chefe.gameObject.SetActive(true);
        LimparPerigos(); pontos = 0; energia = 40; tempo = 0f; acertos = coletas = 0;
        proximoAlien = 9f; tremor = 0f;
        Vidas = VidasIniciais; protegidoAte = 0f;
        Jogador.Reiniciar(new Vector3(-5f, -2.4f, 0f));
        Chefe.Reiniciar(); bola.Recolher(false); AlterarEstado(Estado.Jogando);
        Avisar("ROUND 1 - AQUECIMENTO", 2f);
    }
    public void Machucar()
    {
        if (!EmJogo || Invulneravel) return;
        Vidas = Mathf.Max(0, Vidas - 1);
        protegidoAte = Time.time + 1.5f;
        tremor = 0.3f; Som(3);
        if (Vidas == 0) { SalvarRecorde(); AlterarEstado(Estado.Derrota); }
        else Avisar("Cuidado! " + Vidas + " vidas restantes", 1.2f);
    }
    public void Vencer()
    {
        pontos += 1000 + Mathf.Max(0, 600 - Mathf.FloorToInt(tempo * 2f));
        Chefe.gameObject.SetActive(false);
        Som(4); SalvarRecorde(); AlterarEstado(Estado.Vitoria);
    }
    private void SalvarRecorde()
    {
        recorde = Mathf.Max(recorde, pontos); PlayerPrefs.SetInt("QuadraLunar.Recorde", recorde); PlayerPrefs.Save();
    }
    public void AcertarChefe(int dano, bool rebote, bool cabeca = false)
    {
        if (!Chefe.PodeReceberDano) return;
        acertos++; pontos += rebote ? 180 : 100; energia = Mathf.Min(100, energia + 12);
        tremor = 0.35f; Som(1); Avisar(rebote ? "REBOTE! Dano e pontos extras" : "+100  |  +12 energia", 1f);
        if (cabeca) { dano *= 2; pontos += 75; Avisar("NA CABECA! Dano x2  |  +75 pontos", 1.3f); }
        Chefe.ReceberDano(dano);
    }
    public void RecompensarColeta() { coletas++; pontos += 25; }
    public void AlienDerrotado()
    {
        pontos += 75; energia = Mathf.Min(100, energia + 8); Som(2);
        if (RecarregaAoMatar) { bola.Recolher(false); Avisar("INIMIGO ELIMINADO - bola recarregada!", 1f); }
    }
    public void RecompensaFase()
    {
        pontos += 300;
        CriarPerigo(new Vector2(-3f, -2.4f), Vector2.zero, 0f, true);
        Som(4);
    }
    public void ColetarEstrela() { energia = Mathf.Min(100, energia + 25); pontos += 150; Som(2); Avisar("CRISTAL! +25 energia / +150 pontos", 1.5f); }
    public void Som(int indice, float volume = 0.6f)
    {
        if (efeitos != null && indice < efeitos.Length && efeitos[indice] != null) audioFonte.PlayOneShot(efeitos[indice], volume);
    }
    public void Avisar(string mensagem, float duracao) { aviso = mensagem; avisoAte = Time.time + duracao; }
    public PerigoQuadra CriarPerigo(Vector2 posicao, Vector2 velocidade, float antecipacao, bool coleta = false)
    {
        Sprite imagem = coleta ? cristalSprite : projetilInimigoSprite;
        float escala = (coleta ? 0.6f : 0.46f) / Mathf.Max(imagem.bounds.size.x, imagem.bounds.size.y);
        var go = Visual(coleta ? "Cristal de energia" : "Esfera de fogo inimiga", imagem, posicao, Vector2.one * escala, Color.white, 7);
        go.transform.SetParent(perigos);
        var perigo = go.AddComponent<PerigoQuadra>();
        perigo.Configurar(this, velocidade, antecipacao, coleta);
        return perigo;
    }
    public void MarcarColuna(float x)
    {
        var go = Bloco("Aviso de chuva", new Vector2(x, 0.6f), new Vector2(0.6f, 7.2f), new Color(1f, 0.72f, 0.18f, 0.22f), 2);
        go.transform.SetParent(perigos); Destroy(go, 1f);
        // As bordas mostram a largura da queda, inclusive nas plataformas.
        for (int lado = -1; lado <= 1; lado += 2)
        {
            var borda = Bloco("Limite da chuva", new Vector2(x + lado * 0.3f, 0.6f), new Vector2(0.035f, 7.2f), new Color(1f, 0.75f, 0.2f, 0.8f), 3);
            borda.transform.SetParent(perigos); Destroy(borda, 1f);
        }
    }
    public void LimparPerigos()
    {
        foreach (Transform filho in perigos) { filho.gameObject.SetActive(false); Destroy(filho.gameObject); }
    }
    private void OnApplicationFocus(bool foco) { if (!foco && EmJogo) AlterarEstado(Estado.Pausado); }
    private void OnDestroy()
    {
        Time.timeScale = 1f;
        if (MaterialEfeito != null) Destroy(MaterialEfeito);
        if (tile != null) Destroy(tile);
    }
    private void OnGUI()
    {
        if (Jogador == null || bola == null) return;
        if (!estilosProntos)
        {
            titulo = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            texto = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            pequeno = new GUIStyle(texto) { fontSize = 15 };
            botao = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            estilosProntos = true;
        }
        Matrix4x4 anterior = GUI.matrix;
        DesenharVidas();
        float escala = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280f * escala) / 2f, (Screen.height - 720f * escala) / 2f), Quaternion.identity, Vector3.one * escala);
        Painel(new Rect(30, 20, 1220, 108), new Color(0.035f, 0.05f, 0.1f, 0.94f));
        Rotulo(new Rect(45, 28, 270, 32), "QUADRA LUNAR", texto);
        Rotulo(new Rect(45, 62, 270, 24), "PONTOS " + pontos.ToString("00000") + "   |   RECORDE " + recorde, pequeno);
        Rotulo(new Rect(350, 25, 570, 30), "CAPITAO ORBITA  /  " + NomesFases[Chefe.Fase - 1], texto);
        Painel(new Rect(360, 65, 550, 15), new Color(0.2f, 0.23f, 0.3f));
        Painel(new Rect(360, 65, 550f * Chefe.Vida / 40f, 15), new Color(1f, 0.42f, 0.28f));
        for (int i = 1; i < 4; i++) Painel(new Rect(360 + i * 137.5f, 65, 3, 15), Color.black);
        Rotulo(new Rect(950, 28, 280, 30), "ENERGIA " + energia + "/100", texto);
        Rotulo(new Rect(940, 64, 290, 24), "E: ESPECIAL  /  CUSTO 40", pequeno);
        string status = bola.NaMao ? "BOLA NA MAO" : bola.Retornando ? "BOLA VOLTANDO" : "RECUPERE A BOLA  /  retorno em " + bola.SegundosParaVoltar.ToString("0.0") + "s";
        status = Dificuldade.ToString().ToUpperInvariant() + " | " + status;
        Rotulo(new Rect(320, 92, 640, 25), status, pequeno);
        Painel(new Rect(30, 665, 1220, 40), new Color(0.035f, 0.05f, 0.1f, 0.94f));
        Rotulo(new Rect(40, 670, 1200, 30), "A/D: mover   Segure ESPACO: pulo alto   S+ESPACO: descer   SHIFT: deslizar   MOUSE: mirar/atirar   J: atirar   E: especial   ESC: pausa", pequeno);
        if (EmJogo)
        {
            Rotulo(new Rect(180, 127, 920, 23), "BASQUETE: SUA BOLA    |    FOGO VERMELHO: DESVIE    |    CRISTAL VERDE: COLETE", pequeno);
            if (Time.time < avisoAte) Rotulo(new Rect(200, 150, 880, 40), aviso, texto);
            Rotulo(new Rect(250, 620, 780, 30), Chefe.Intencao, pequeno);
        }
        else
        {
            Painel(new Rect(240, 180, 800, 445), new Color(0.035f, 0.05f, 0.12f, 0.98f));
            string cabecalho = estado == Estado.Menu ? "QUADRA LUNAR" : estado == Estado.Pausado ? "INTERVALO" : estado == Estado.Vitoria ? "VITORIA!" : "GAME OVER";
            Rotulo(new Rect(270, 200, 740, 70), cabecalho, titulo);
            string descricao = estado == Estado.Menu ? "Uma bola. Quatro rounds. Um capitao fora de orbita.\nSobreviva, recupere sua bola e derrote o chefe!" :
                estado == Estado.Pausado ? "Respire. A quadra espera por voce." :
                estado == Estado.Vitoria ? "Voce conquistou a quadra!\nPontos: " + pontos + "  |  Tempo: " + tempo.ToString("0.0") + "s" :
                "Suas vidas acabaram. Tente outra estrategia!\nRound " + Chefe.Fase + "  |  Pontos: " + pontos + "  |  Acertos: " + acertos;
            Rotulo(new Rect(280, 280, 720, 75), descricao, texto);
            if (estado == Estado.Menu)
            {
                string[] nomes = { "FACIL", "MEDIO", "DIFICIL" };
                for (int i = 0; i < 3; i++)
                    if (Botao(new Rect(300 + i * 230, 365, 220, 40), (int)Dificuldade == i ? "[ " + nomes[i] + " ]" : nomes[i], botao)) SelecionarModo((Modo)i);
                Rotulo(new Rect(285, 411, 710, 40), VidasIniciais + " vidas | " + (ProjeteisExtras > 0 ? "Mais tiros | " : "") + (RecarregaAoMatar ? "Matar recarrega a bola" : "Matar NAO recarrega a bola"), pequeno);
            }
            else Rotulo(new Rect(285, 368, 710, 76), "Modo: " + Dificuldade + " | " + VidasIniciais + " vidas iniciais\nNa parede: segure a direcao + W para subir; Espaco salta para fora.\nA bola perdida retorna em ate 6s, em todos os modos.", pequeno);
            if (Botao(new Rect(430, 463, 420, 52), estado == Estado.Pausado ? "CONTINUAR" : estado == Estado.Menu ? "ENTRAR NA QUADRA" : "JOGAR NOVAMENTE", botao))
            {
                if (estado == Estado.Pausado) AlterarEstado(Estado.Jogando);
                else { Chefe.gameObject.SetActive(true); Iniciar(); }
            }
            if (estado != Estado.Menu && Botao(new Rect(430, 530, 420, 42), "MENU", botao)) AlterarEstado(Estado.Menu);
            if (estado == Estado.Menu) Rotulo(new Rect(300, 533, 680, 55), "Parede: direcao + W sobe; Espaco salta para fora.\nProtecao de 1,5s apos dano | F11: tela cheia no jogo exportado", pequeno);
        }
        GUI.matrix = anterior;
    }
    private static void Painel(Rect area, Color cor)
    {
        Color anterior = GUI.color; GUI.color = cor; GUI.DrawTexture(area, Texture2D.whiteTexture); GUI.color = anterior;
    }

    private void DesenharVidas()
    {
        if (!EmJogo && estado != Estado.Pausado) return;
        Vector3 ponto = cameraArena.WorldToScreenPoint(Jogador.transform.position + Vector3.up * 0.88f);
        if (ponto.z <= 0f) return;
        float tamanho = Mathf.Clamp(Screen.height / 65f, 8f, 18f);
        float x = ponto.x - tamanho * (VidasIniciais * 1.25f - .25f) * .5f;
        float y = Screen.height - ponto.y;
        for (int i = 0; i < VidasIniciais; i++)
        {
            var area = new Rect(x + i * tamanho * 1.25f, y, tamanho, tamanho * 0.62f);
            Painel(new Rect(area.x - 2, area.y - 2, area.width + 4, area.height + 4), new Color(0.02f, 0.04f, 0.08f, 0.9f));
            Painel(area, i < Vidas ? new Color(0.3f, 1f, 0.65f) : new Color(0.25f, 0.28f, 0.35f));
        }
    }

    // Calcula o layout em 1280x720, mas rasteriza as fontes no tamanho real da janela.
    private static Rect RetanguloNativo(Rect area, Matrix4x4 matriz)
    {
        Vector3 pos = matriz.MultiplyPoint3x4(new Vector3(area.x, area.y, 0f));
        return new Rect(Mathf.Round(pos.x), Mathf.Round(pos.y), Mathf.Round(area.width * matriz.m00), Mathf.Round(area.height * matriz.m11));
    }
    private static void Rotulo(Rect area, string conteudo, GUIStyle estilo)
    {
        Matrix4x4 matriz = GUI.matrix;
        int fonte = estilo.fontSize;
        estilo.fontSize = Mathf.Max(10, Mathf.RoundToInt(fonte * matriz.m00));
        GUI.matrix = Matrix4x4.identity;
        GUI.Label(RetanguloNativo(area, matriz), conteudo, estilo);
        GUI.matrix = matriz; estilo.fontSize = fonte;
    }
    private static bool Botao(Rect area, string conteudo, GUIStyle estilo)
    {
        Matrix4x4 matriz = GUI.matrix;
        int fonte = estilo.fontSize;
        estilo.fontSize = Mathf.Max(10, Mathf.RoundToInt(fonte * matriz.m00));
        GUI.matrix = Matrix4x4.identity;
        Rect nativo = RetanguloNativo(area, matriz);
        Painel(nativo, nativo.Contains(Event.current.mousePosition) ? new Color(0.18f, 0.48f, 0.53f) : new Color(0.1f, 0.28f, 0.35f));
        bool clicado = GUI.Button(nativo, conteudo, estilo);
        GUI.matrix = matriz; estilo.fontSize = fonte;
        return clicado;
    }
}
