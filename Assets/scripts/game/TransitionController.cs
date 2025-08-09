using UnityEngine;
using UnityEngine.UI;
using System.Collections;

//Este script no hace nada

public class TransitionController : MonoBehaviour
{
	[Header("Configuración de la Transición")]
	public Image imageUITransicion;
	public Material clockWipeMaterial;
	public Material flashbackWipeMaterial;

	[Header("Tiempos de Animación (Segundos)")]
	public float tiempoFeather = 0.5f;
	public float tiempoProgressAparecer = 1.0f;
	public float tiempoProgressDesaparecer = 1.0f;

	[Header("Valores del Shader")]
	public float featherValorActivo = 0.1f;

	[Header("Teclas de Control (Debug)")]
	public KeyCode teclaClockWipeAparecer = KeyCode.L;
	public KeyCode teclaClockWipeDesaparecer = KeyCode.R;
	public KeyCode teclaFlashbackAparecer = KeyCode.X;
	public KeyCode teclaFlashbackDesaparecer = KeyCode.Y;


	private Material materialInstanciado;
	private Coroutine transicionEnCursoCoroutine;
	private Material baseMaterialActualDeInstancia = null;


	private const string PROGRESS_PROPERTY = "_Progress";
	private const string FEATHER_PROPERTY = "_Feather";

	void Start()
	{
		if (imageUITransicion == null)
		{
			Debug.LogError("TransitionController: imageUITransicion no está asignada en el Inspector.", gameObject);
			enabled = false;
			return;
		}
		imageUITransicion.gameObject.SetActive(false);
	}

	void Update()
	{
		if (Input.GetKeyDown(teclaClockWipeAparecer))
		{
			IniciarClockWipeAparecer();
		}
		if (Input.GetKeyDown(teclaClockWipeDesaparecer))
		{
			IniciarClockWipeDesaparecer();
		}
		if (Input.GetKeyDown(teclaFlashbackAparecer))
		{
			IniciarFlashbackWipeAparecer();
		}
		if (Input.GetKeyDown(teclaFlashbackDesaparecer))
		{
			IniciarFlashbackWipeDesaparecer();
		}
	}

	private void DetenerTransicionAnterior()
	{
		if (transicionEnCursoCoroutine != null)
		{
			StopCoroutine(transicionEnCursoCoroutine);
			transicionEnCursoCoroutine = null;
		}
	}

	private bool PrepararParaTransicion(Material materialBase, float progressInicial, float featherInicial)
	{
		if (imageUITransicion == null || materialBase == null)
		{
			Debug.LogError("No se puede preparar la transición: Image UI o Material Base no asignados.");
			return false;
		}

		DetenerTransicionAnterior();
		if (materialInstanciado == null || baseMaterialActualDeInstancia != materialBase)
		{
			Debug.LogWarning("PrepararParaTransicion: Creando nueva instancia de material basada en: " + materialBase.name);
			if (materialInstanciado != null)
			{
				Destroy(materialInstanciado);
			}
			materialInstanciado = new Material(materialBase);
			baseMaterialActualDeInstancia = materialBase;
		}

		imageUITransicion.material = materialInstanciado;

		materialInstanciado.SetFloat(PROGRESS_PROPERTY, progressInicial);
		materialInstanciado.SetFloat(FEATHER_PROPERTY, featherInicial);

		imageUITransicion.gameObject.SetActive(true);
		return true;
	}

	public void IniciarClockWipeAparecer()
	{
		if (!PrepararParaTransicion(clockWipeMaterial, 1f, 0f)) return;

		Debug.Log("TransitionController: Iniciando ClockWipe APARECER.");
		transicionEnCursoCoroutine = StartCoroutine(
			AnimateTransitionProperties(0f, featherValorActivo, tiempoProgressAparecer, tiempoFeather, false) // false = no desactivar al finalizar
		);
	}

	public void IniciarClockWipeDesaparecer()
	{
		if (!PrepararParaTransicion(clockWipeMaterial, 0f, featherValorActivo)) return;

		Debug.Log("TransitionController: Iniciando ClockWipe DESAPARECER.");

		transicionEnCursoCoroutine = StartCoroutine(
			AnimateDisappearSequenceCoroutine(
				0f,
				1f,
				tiempoProgressDesaparecer,
				featherValorActivo,
				0f,
				tiempoFeather,
				true
			)
		);
	}

	// --- FLASHBACK WIPE ---
	public void IniciarFlashbackWipeAparecer()
	{

		if (!PrepararParaTransicion(flashbackWipeMaterial, 1f, 0f)) return;

		Debug.Log("TransitionController: Iniciando FlashbackWipe APARECER.");

		transicionEnCursoCoroutine = StartCoroutine(
			AnimateTransitionProperties(0f, featherValorActivo, tiempoProgressAparecer, tiempoFeather, false)
		);
	}

	public void IniciarFlashbackWipeDesaparecer()
	{
		if (!PrepararParaTransicion(flashbackWipeMaterial, 0f, featherValorActivo)) return;

		Debug.Log("TransitionController: Iniciando FlashbackWipe DESAPARECER.");
		transicionEnCursoCoroutine = StartCoroutine(
			AnimateDisappearSequenceCoroutine(
				0f,
				1f,
				tiempoProgressDesaparecer,
				featherValorActivo,
				0f,
				tiempoFeather,
				true
			)
		);
	}

	// --- CORRUTINA GENÉRICA DE ANIMACIÓN DE PROPIEDADES ---
	private IEnumerator AnimateTransitionProperties(float progressObjetivo, float featherObjetivo, float duracionProgress, float duracionFeather, bool desactivarAlFinalizar)
	{
		if (materialInstanciado == null)
		{
			Debug.LogError("AnimateTransitionProperties: materialInstanciado es null.");
			transicionEnCursoCoroutine = null;
			yield break;
		}

		float progressInicial = materialInstanciado.GetFloat(PROGRESS_PROPERTY);
		float featherInicial = materialInstanciado.GetFloat(FEATHER_PROPERTY);

		float tiempoTotalAnimacion = Mathf.Max(duracionProgress, duracionFeather);
		if (tiempoTotalAnimacion <= 0)
		{
			materialInstanciado.SetFloat(PROGRESS_PROPERTY, progressObjetivo);
			materialInstanciado.SetFloat(FEATHER_PROPERTY, featherObjetivo);
			Debug.Log("AnimateTransitionProperties: Transición instantánea aplicada.");
			if (desactivarAlFinalizar && progressObjetivo >= 1f) // Solo desactivar si es una desaparición completa
			{
				imageUITransicion.gameObject.SetActive(false);
			}
			transicionEnCursoCoroutine = null;
			yield break;
		}

		float tiempoTranscurrido = 0f;

		while (tiempoTranscurrido < tiempoTotalAnimacion)
		{
			tiempoTranscurrido += Time.deltaTime;

			if (duracionProgress > 0)
			{
				float tProgress = Mathf.Clamp01(tiempoTranscurrido / duracionProgress);
				float nuevoProgress = Mathf.Lerp(progressInicial, progressObjetivo, tProgress);
				materialInstanciado.SetFloat(PROGRESS_PROPERTY, nuevoProgress);
			}
			else
			{
				materialInstanciado.SetFloat(PROGRESS_PROPERTY, progressObjetivo);
			}

			if (duracionFeather > 0)
			{
				float tFeather = Mathf.Clamp01(tiempoTranscurrido / duracionFeather);
				float nuevoFeather = Mathf.Lerp(featherInicial, featherObjetivo, tFeather);
				materialInstanciado.SetFloat(FEATHER_PROPERTY, nuevoFeather);
			}
			else
			{
				materialInstanciado.SetFloat(FEATHER_PROPERTY, featherObjetivo);
			}

			yield return null;
		}

		materialInstanciado.SetFloat(PROGRESS_PROPERTY, progressObjetivo);
		materialInstanciado.SetFloat(FEATHER_PROPERTY, featherObjetivo);

		Debug.Log("AnimateTransitionProperties: Transición (Aparecer) completada.");

		if (desactivarAlFinalizar && progressObjetivo >= 1f)
		{
			imageUITransicion.gameObject.SetActive(false);
			Debug.Log("AnimateTransitionProperties: imageUITransicion desactivada (porque era una desaparición).");
		}

		transicionEnCursoCoroutine = null;
	}

	private IEnumerator AnimateDisappearSequenceCoroutine(
		float progressInicial, float progressObjetivo, float duracionProgress,
		float featherMantenido, float featherObjetivoFinal, float duracionFeather,
		bool desactivarAlFinalizar)
	{
		if (materialInstanciado == null)
		{
			Debug.LogError("AnimateDisappearSequenceCoroutine: materialInstanciado es null.");
			transicionEnCursoCoroutine = null;
			yield break;
		}

		float tiempoTranscurrido = 0f;

		Debug.Log(string.Format("AnimateDisappearSequenceCoroutine - Fase 1: Animando Progress de {0} a {1} (dur: {2}s). Feather se mantiene en {3}.",
			progressInicial, progressObjetivo, duracionProgress, featherMantenido));

		materialInstanciado.SetFloat(FEATHER_PROPERTY, featherMantenido); // Aseguramos el valor de feather

		if (duracionProgress > 0)
		{
			while (tiempoTranscurrido < duracionProgress)
			{
				tiempoTranscurrido += Time.deltaTime;
				float tProgress = Mathf.Clamp01(tiempoTranscurrido / duracionProgress);
				float nuevoProgress = Mathf.Lerp(progressInicial, progressObjetivo, tProgress);
				materialInstanciado.SetFloat(PROGRESS_PROPERTY, nuevoProgress);
				yield return null;
			}
		}
		materialInstanciado.SetFloat(PROGRESS_PROPERTY, progressObjetivo); // Aseguramos valor final de Progress
		Debug.Log("AnimateDisappearSequenceCoroutine - Fase 1: Progress completado.");

		Debug.Log(string.Format("AnimateDisappearSequenceCoroutine - Fase 2: Animando Feather de {0} a {1} (dur: {2}s). Progress está en {3}.",
			featherMantenido, featherObjetivoFinal, duracionFeather, materialInstanciado.GetFloat(PROGRESS_PROPERTY)));

		tiempoTranscurrido = 0f; // Reseteamos para la animación de feather
		float featherActualAlIniciarFase2 = materialInstanciado.GetFloat(FEATHER_PROPERTY); // Debería ser featherMantenido

		if (duracionFeather > 0)
		{
			while (tiempoTranscurrido < duracionFeather)
			{
				tiempoTranscurrido += Time.deltaTime;
				float tFeather = Mathf.Clamp01(tiempoTranscurrido / duracionFeather);
				float nuevoFeather = Mathf.Lerp(featherActualAlIniciarFase2, featherObjetivoFinal, tFeather);
				materialInstanciado.SetFloat(FEATHER_PROPERTY, nuevoFeather);
				yield return null;
			}
		}
		materialInstanciado.SetFloat(FEATHER_PROPERTY, featherObjetivoFinal); // Aseguramos valor final de Feather
		Debug.Log("AnimateDisappearSequenceCoroutine - Fase 2: Feather completado.");

		if (desactivarAlFinalizar)
		{
			imageUITransicion.gameObject.SetActive(false);
			Debug.Log("AnimateDisappearSequenceCoroutine: imageUITransicion desactivada.");
		}

		transicionEnCursoCoroutine = null;
	}
}
