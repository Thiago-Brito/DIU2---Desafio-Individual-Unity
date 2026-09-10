using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BolaBasquete : MonoBehaviour
{
    [SerializeField] private PlayerController2D jogador;
    [SerializeField] private Sprite[] imagensDaBola;
    [SerializeField] private float velocidadeDoQuique = 7f;
    [SerializeField] private float alturaDoQuique = 0.48f;
    [SerializeField] private float distanciaDoJogador = 0.34f;

    private SpriteRenderer desenhoDaBola;
    private SpriteRenderer desenhoDoJogador;
    private float tempo;

    private void Awake()
    {
        desenhoDaBola = GetComponent<SpriteRenderer>();
        if (jogador != null) desenhoDoJogador = jogador.GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (jogador == null || desenhoDoJogador == null) return;

        // Mantém a bola sempre na frente do personagem.
        float lado = desenhoDoJogador.flipX ? -1f : 1f;
        float altura = 0.08f;

        bool podeQuicar = jogador.EstaNoChao && jogador.EstaMovendo &&
                          !jogador.EstaAbaixado && !jogador.EstaDeslizando;

        if (podeQuicar)
        {
            tempo += Time.deltaTime * velocidadeDoQuique;
            altura = -0.18f + Mathf.Abs(Mathf.Sin(tempo)) * alturaDoQuique;
        }
        else
        {
            tempo = 0f;
        }

        transform.localPosition = new Vector3(lado * distanciaDoJogador, altura, -0.1f);

        // Os quatro desenhos dão a impressão de que a bola está girando.
        if (imagensDaBola != null && imagensDaBola.Length > 0)
        {
            int quadro = Mathf.FloorToInt(tempo * 1.5f) % imagensDaBola.Length;
            desenhoDaBola.sprite = imagensDaBola[quadro];
        }
    }
}
