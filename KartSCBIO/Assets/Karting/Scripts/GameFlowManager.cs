using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using KartGame.KartSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using UnityEngine.UI;

public enum GameState{Play, Won, Lost}

public class GameFlowManager : MonoBehaviour
{
    [Header("Configuration Race")]
    public ConfigurationRace configuration;
    [Header("Parameters")]
    [Tooltip("Duration of the fade-to-black at the end of the game")]
    public float endSceneLoadDelay = 3f;
    [Tooltip("The canvas group of the fade-to-black screen")]
    public CanvasGroup endGameFadeCanvasGroup;
    [Header("Win")]
    [Tooltip("This string has to be the name of the scene you want to load when winning")]
    public string winSceneName = "WinScene";
    [Tooltip("Duration of delay before the fade-to-black, if winning")]
    public float delayBeforeFadeToBlack = 4f;
    [Tooltip("Duration of delay before the win message")]
    public float delayBeforeWinMessage = 2f;
    [Tooltip("Sound played on win")]
    public AudioClip victorySound;

    [Tooltip("Prefab for the win game message")]
    public DisplayMessage winDisplayMessage;

    public PlayableDirector raceCountdownTrigger;

    [Header("Lose")]
    [Tooltip("This string has to be the name of the scene you want to load when losing")]
    public string loseSceneName = "LoseScene";
    [Tooltip("Prefab for the lose game message")]
    public DisplayMessage loseDisplayMessage;
    public GameState gameState { get; private set; }
    public bool autoFindKarts = true;
    public ArcadeKart playerKart;
    public Text textRank;
    public Image ImageItem;
    public GameObject VictoryPanel;
    public List<Collider> CheckpointsRanks = new List<Collider>();
    public List<Sprite> IconSprites = new List<Sprite>();
    public List<GameObject> ItemsPrefabs = new List<GameObject>(4);
    public Image WheelSteering;
    public bool isMultiplayer;
    ArcadeKart[] karts;
    ArcadeKart[] kartsMultiplayer;
    ObjectiveManager m_ObjectiveManager;
    TimeManager m_TimeManager;
    float m_TimeLoadEndGameScene;
    string m_SceneToLoad;
    float elapsedTimeBeforeEndScene = 0;
    void Start()
    {
        if (autoFindKarts)
        {
            karts = FindObjectsOfType<ArcadeKart>();
            if (karts.Length > 0)
            {
                if (!playerKart) playerKart = karts[0];
            }
            DebugUtility.HandleErrorIfNullFindObject<ArcadeKart, GameFlowManager>(playerKart, this);
        }

        m_ObjectiveManager = FindObjectOfType<ObjectiveManager>();
		DebugUtility.HandleErrorIfNullFindObject<ObjectiveManager, GameFlowManager>(m_ObjectiveManager, this);

        m_TimeManager = FindObjectOfType<TimeManager>();
        DebugUtility.HandleErrorIfNullFindObject<TimeManager, GameFlowManager>(m_TimeManager, this);

        AudioUtility.SetMasterVolume(1);

        winDisplayMessage.gameObject.SetActive(false);
        loseDisplayMessage.gameObject.SetActive(false);

        m_TimeManager.StopRace();
        foreach (ArcadeKart k in karts)
        {
			k.SetCanMove(false);
        }

        //run race countdown animation
        ShowRaceCountdownAnimation();
        StartCoroutine(ShowObjectivesRoutine());

        StartCoroutine(CountdownThenStartRaceRoutine());
    }

    IEnumerator CountdownThenStartRaceRoutine() {
        yield return new WaitForSeconds(3f);
        StartRace();
    }

    void StartRace() {
        foreach (ArcadeKart k in karts)
        {
			k.SetCanMove(true);
        }
        m_TimeManager.StartRace();
    }

    void ShowRaceCountdownAnimation() {
        raceCountdownTrigger.Play();
    }

    IEnumerator ShowObjectivesRoutine() {
        while (m_ObjectiveManager.Objectives.Count == 0)
            yield return null;
        yield return new WaitForSecondsRealtime(0.2f);
        for (int i = 0; i < m_ObjectiveManager.Objectives.Count; i++)
        {
           if (m_ObjectiveManager.Objectives[i].displayMessage)m_ObjectiveManager.Objectives[i].displayMessage.Display();
           yield return new WaitForSecondsRealtime(1f);
        }
    }


    void Update()
    {
        if(playerKart) 
        {
            if(playerKart.Input.TurnInput > 0.5f) 
            {
                WheelSteering.rectTransform.rotation = Quaternion.Slerp(WheelSteering.rectTransform.rotation, new Quaternion(0,0,-1f,1), Time.deltaTime * 1.5f);
            } else if(playerKart.Input.TurnInput < -0.5f)
            {
                WheelSteering.rectTransform.rotation = Quaternion.Slerp(WheelSteering.rectTransform.rotation, new Quaternion(0,0,1f,1), Time.deltaTime * 1.5f);
            } else 
            {
                WheelSteering.rectTransform.rotation = Quaternion.Slerp(WheelSteering.rectTransform.rotation, new Quaternion(0,0,0,1), Time.deltaTime * 5f);
            }
        }
        if (gameState != GameState.Play)
        {
            elapsedTimeBeforeEndScene += Time.deltaTime;
            if(elapsedTimeBeforeEndScene >= endSceneLoadDelay)
            {

                float timeRatio = 1 - (m_TimeLoadEndGameScene - Time.time) / endSceneLoadDelay;
                endGameFadeCanvasGroup.alpha = timeRatio;

                float volumeRatio = Mathf.Abs(timeRatio);
                float volume = Mathf.Clamp(1 - volumeRatio, 0, 1);
                AudioUtility.SetMasterVolume(volume);

                // See if it's time to load the end scene (after the delay)
                if (Time.time >= m_TimeLoadEndGameScene)
                {
                    SceneManager.LoadScene(m_SceneToLoad);
                    gameState = GameState.Play;
                }
            }
        }
        else
        {
            if(!isMultiplayer)
            {
                if (m_ObjectiveManager.AreAllObjectivesCompleted())
                    EndGame(true);

                if (m_TimeManager.IsFinite && m_TimeManager.IsOver)
                    EndGame(false);
            } else 
            {
                if(m_ObjectiveManager.AreAllObjectivesCompleted()) 
                {
                    Photon.Pun.PhotonNetwork.Disconnect();
                    SceneManager.LoadScene("Main Menu");
                }
            }
        }
    }
    public void RankRacers(bool multiplayer) 
    {
        bool swap = true;
        while(swap == true)
        {
            swap = false;
            for (int i = karts.Length - 1; i > 0; i--)
            {
                if(karts[i].GetComponent<CheckPointTracker>().checkpointsPassed > karts[i-1].GetComponent<CheckPointTracker>().checkpointsPassed) 
                {
                    swap = true;
                    ArcadeKart tempKart = karts[i-1];
                    karts[i-1] = karts[i];
                    karts[i] = tempKart;
                }
            }
        }
        if(!multiplayer)
        {
            int index = FindRankIndex(playerKart);
            textRank.text = (index+1).ToString() + "º";
        }
    }
    public int FindRankIndex(ArcadeKart kart) 
    {
        for (int i = 0; i < karts.Length; i++)
        {
            if(karts[i] == kart)
            {
                return i;
            }
        }
        return 0;
    }
    public Transform FindNextRank(int index) 
    {
        if(index != 0)
        {
            return karts[index-1].transform;
        }
        return karts[karts.Length-1].transform;
    }
    public Transform FindFirstRank(int index)
    {
        if(index != 0)
        {
            return karts[0].transform;
        }
        return karts[karts.Length-1].transform;
    }
    void EndGame(bool win)
    {
        // unlocks the cursor before leaving the scene, to be able to click buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        m_TimeManager.StopRace();
        configuration.rankPlayer = FindRankIndex(playerKart) + 1;
        // Remember that we need to load the appropriate end scene after a delay
        gameState = win ? GameState.Won : GameState.Lost;
        endGameFadeCanvasGroup.gameObject.SetActive(true);
        if (win)
        {
            m_SceneToLoad = winSceneName;
            m_TimeLoadEndGameScene = Time.time + endSceneLoadDelay + delayBeforeFadeToBlack;

            // play a sound on win
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = victorySound;
            audioSource.playOnAwake = false;
            audioSource.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.HUDVictory);
            audioSource.PlayScheduled(AudioSettings.dspTime + delayBeforeWinMessage);

            // create a game message
            winDisplayMessage.delayBeforeShowing = delayBeforeWinMessage;
            winDisplayMessage.gameObject.SetActive(true);
        }
        else
        {
            playerKart.SetCanMove(false);
            m_SceneToLoad = loseSceneName;
            m_TimeLoadEndGameScene = Time.time + endSceneLoadDelay + delayBeforeFadeToBlack;

            // create a game message
            loseDisplayMessage.delayBeforeShowing = delayBeforeWinMessage;
            loseDisplayMessage.gameObject.SetActive(true);
        }
    }
}
