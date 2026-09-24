using UnityEngine;

public class PerigoQuadra : MonoBehaviour
{
    private ArenaBasquete jogo;
    private Vector2 velocidade;
    private float aviso, vida;
    private bool coleta;
    private SpriteRenderer desenho;
    private Rigidbody2D corpo;
    private CircleCollider2D colisor;
    private LineRenderer anel;
    private TrailRenderer rastro;
    private float duracaoAviso, escalaLinha;
    private Vector3 escalaOriginal;
    public bool EmPreparacao => !coleta && aviso > 0f;
    public bool Coletavel => coleta;
    private static readonly Color CorPerigo = new Color(1f, 0.2f, 0.25f);
    private static readonly Color CorAviso = new Color(1f, 0.85f, 0.3f);
    public void Configurar(ArenaBasquete arena, Vector2 movimento, float antecipacao, bool recompensa = false)
    {
        jogo = arena; velocidade = movimento; aviso = recompensa ? 0f : Mathf.Max(0f, antecipacao); duracaoAviso = aviso; vida = 7f; coleta = recompensa;
        desenho = GetComponent<SpriteRenderer>();
        escalaOriginal = transform.localScale;
        escalaLinha = 1f / transform.lossyScale.x;
        colisor = gameObject.AddComponent<CircleCollider2D>();
        // Mantem a area de dano antiga (~0,16 mundo); o contorno luminoso e apenas visual.
        colisor.radius = (coleta ? 0.23f : 0.161f) * escalaLinha; colisor.isTrigger = true;
        colisor.enabled = !EmPreparacao;
        corpo = gameObject.AddComponent<Rigidbody2D>();
        corpo.bodyType = RigidbodyType2D.Kinematic;
        corpo.useFullKinematicContacts = true;
        corpo.interpolation = RigidbodyInterpolation2D.Interpolate;
        if (coleta)
        {
            // Losango verde: silhueta diferente tanto da bola quanto do fogo.
            anel = Linha("Contorno do cristal", new[] { new Vector3(0, .43f), new Vector3(.43f, 0), new Vector3(0, -.43f), new Vector3(-.43f, 0) }, new Color(.3f, 1f, .75f), true);
            desenho.color = Color.white;
        }
        else
        {
            Vector3[] circulo = new Vector3[32];
            for (int i = 0; i < circulo.Length; i++)
            {
                float angulo = i * Mathf.PI * 2f / circulo.Length;
                circulo[i] = new Vector3(Mathf.Cos(angulo), Mathf.Sin(angulo)) * .5f;
            }
            anel = Linha("Anel de carga", circulo, CorAviso, true);
            rastro = gameObject.AddComponent<TrailRenderer>();
            rastro.sharedMaterial = jogo.MaterialEfeito; rastro.time = .18f;
            rastro.startWidth = .14f; rastro.endWidth = 0f;
            rastro.startColor = CorPerigo; rastro.endColor = Color.clear; rastro.sortingOrder = 6;
            rastro.emitting = !EmPreparacao;
            desenho.color = EmPreparacao ? CorAviso : CorPerigo;
            anel.enabled = EmPreparacao;
        }
    }
    private LineRenderer Linha(string nome, Vector3[] pontos, Color cor, bool fechar)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(transform, false);
        objeto.transform.localScale = Vector3.one * escalaLinha;
        var linha = objeto.AddComponent<LineRenderer>();
        linha.sharedMaterial = jogo.MaterialEfeito; linha.useWorldSpace = false;
        linha.positionCount = pontos.Length; linha.SetPositions(pontos); linha.loop = fechar;
        linha.startWidth = linha.endWidth = .035f;
        linha.startColor = linha.endColor = cor; linha.sortingOrder = 8;
        linha.numCapVertices = 3; linha.numCornerVertices = 3;
        return linha;
    }
    private void FixedUpdate()
    {
        if (!jogo.EmJogo) return;
        if (aviso > 0f)
        {
            aviso = Mathf.Max(0f, aviso - Time.fixedDeltaTime);
            float carga = 1f - aviso / Mathf.Max(.01f, duracaoAviso);
            desenho.color = Color.Lerp(CorAviso, Color.white, carga * .6f);
            anel.transform.localScale = Vector3.one * escalaLinha * Mathf.Lerp(1f, .55f, carga);
            if (aviso == 0f)
            {
                anel.enabled = false;
                colisor.enabled = true; rastro.Clear(); rastro.emitting = true;
                desenho.color = CorPerigo;
            }
            return;
        }
        desenho.color = coleta ? Color.white : CorPerigo;
        if (coleta)
        {
            // Respira sem piscar como os avisos de perigo.
            transform.localScale = escalaOriginal * (1f + Mathf.Sin(Time.time * 3f) * .06f);
            anel.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 2f) * 8f);
        }
        corpo.MovePosition(corpo.position + velocidade * Time.fixedDeltaTime);
        vida -= Time.fixedDeltaTime;
        if (vida <= 0f || Mathf.Abs(transform.position.x) > 11f || transform.position.y < -4f) Destroy(gameObject);
    }
    private void OnTriggerStay2D(Collider2D outro)
    {
        if (!jogo.EmJogo || aviso > 0f || outro.GetComponent<PlayerController2D>() == null) return;
        if (coleta) jogo.ColetarEstrela(); else jogo.Machucar();
        Destroy(gameObject);
    }
}
