using UnityEngine;

public class CameraSeguir : MonoBehaviour
{
    [SerializeField] private Transform jogador;
    [SerializeField] private float suavidade = 5f;

    public void Configurar(Transform alvo) => jogador = alvo;

    private void LateUpdate()
    {
        if (jogador == null) return;
        Vector3 destino = new Vector3(Mathf.Max(0f, jogador.position.x + 2f), 1.5f, -10f);
        Vector3 novaPosicao = Vector3.Lerp(transform.position, destino, suavidade * Time.deltaTime);

        // Os sprites usam 100 pixels por unidade. Arredondar a câmera evita
        // que ela pare entre pixels e deixe o personagem borrado ao andar.
        novaPosicao.x = Mathf.Round(novaPosicao.x * 100f) / 100f;
        novaPosicao.y = Mathf.Round(novaPosicao.y * 100f) / 100f;
        transform.position = novaPosicao;
    }
}
