using UnityEngine;

public class BolaProjetil : MonoBehaviour
{
    private Collider2D dono;
    private Sprite[] quadros;
    private SpriteRenderer desenho;
    private float lado, tempo;
    private readonly RaycastHit2D[] impactos = new RaycastHit2D[32];

    public void Configurar(Collider2D jogador, Sprite[] imagens, float direcao)
    {
        dono = jogador;
        quadros = imagens;
        lado = direcao;
        desenho = GetComponent<SpriteRenderer>();
    }

    private void FixedUpdate()
    {
        float distancia = 15f * Time.fixedDeltaTime;
        Vector2 direcao = Vector2.right * lado;
        // Varredura evita atravessar inimigos mesmo quando a bola anda muito em um frame.
        ContactFilter2D filtro = new ContactFilter2D { useTriggers = true };
        int total = Physics2D.CircleCast(transform.position, 0.12f, direcao, filtro, impactos, distancia);
        RaycastHit2D? primeiro = null;
        for (int i = 0; i < total; i++)
        {
            Collider2D colisor = impactos[i].collider;
            if (colisor == dono || !colisor.gameObject.activeInHierarchy) continue;
            AlienBlue alvo = colisor.GetComponent<AlienBlue>();
            if (alvo != null && colisor != alvo.EsferaBranca) continue;
            if (colisor.isTrigger && alvo == null) continue;
            if (!primeiro.HasValue || impactos[i].distance < primeiro.Value.distance) primeiro = impactos[i];
        }
        if (primeiro.HasValue)
        {
            AlienBlue alien = primeiro.Value.collider.GetComponent<AlienBlue>();
            if (alien != null) alien.Morrer();
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        transform.position += (Vector3)(direcao * distancia);
        tempo += Time.fixedDeltaTime;
        desenho.sprite = quadros[Mathf.FloorToInt(tempo * 16f) % quadros.Length];
        if (tempo > 2f) Destroy(gameObject);
    }
}
