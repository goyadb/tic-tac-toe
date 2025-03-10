using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private GameObject signinPanel;
    [SerializeField] private GameObject signupPanel;
    
    private GameUIController _gameUIController;
    private Canvas _canvas;
    
    private Constants.GameType _gameType;
    
    private MultiplayManager _multiplayManager;

    private void Start()
    {
        // 로그인
        // OpenSigninPanel();
    }

    public void ChangeToGameScene(Constants.GameType gameType)
    {
        _gameType = gameType;
        SceneManager.LoadScene("Game");
    }

    public void ChangeToMainScene()
    {
        SceneManager.LoadScene("Main");
    }

    public void OpenSettingsPanel()
    {
        if (_canvas != null)
        {
            var settingsPanelObject = Instantiate(settingsPanel, _canvas.transform);
            settingsPanelObject.GetComponent<PanelController>().Show();
        }
    }

    public void OpenConfirmPanel(string message, ConfirmPanelController.OnConfirmButtonClick onConfirmButtonClick)
    {
        if (_canvas != null)
        {
            var confirmPanelObject = Instantiate(confirmPanel, _canvas.transform);
            confirmPanelObject.GetComponent<ConfirmPanelController>()
                .Show(message, onConfirmButtonClick);
        }
    }

    public void OpenSigninPanel()
    {
        if (_canvas != null)
        {
            var signinPanelObject = Instantiate(signinPanel, _canvas.transform);
        }
    }

    public void OpenSignupPanel()
    {
        if (_canvas != null)
        {
            var signupPanelObject = Instantiate(signupPanel, _canvas.transform);
        }
    }

    public void OpenGameOverPanel()
    {
        _gameUIController.SetGameUIMode(GameUIController.GameUIMode.GameOver);
    }
    
    protected override void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Game")
        {
            // 씬에 배치된 오브젝트 찾기 (BlockContorller, GameUIController)
            var blockController = GameObject.FindObjectOfType<BlockController>();
            _gameUIController = GameObject.FindObjectOfType<GameUIController>();
            
            // Block Controller 초기화
            blockController.InitBlocks();
            
            // Game UI 초기화
            _gameUIController.SetGameUIMode(GameUIController.GameUIMode.Init);
            
            // Multiplay Manager 생성
            _multiplayManager = new MultiplayManager((state, roomId) =>
            {
                switch (state)
                {
                    case Constants.MultiplayManagerState.CreateRoom:
                        Debug.Log("## Create Room");
                        // TODO: 대기 화면 표시
                        break;
                    case Constants.MultiplayManagerState.JoinRoom:
                        Debug.Log("## Join Room");
                        // 게임 실행
                        
                        break;
                    case Constants.MultiplayManagerState.StartGame:
                        Debug.Log("## Start Game");
                        // 대기 화면을 닫고, 게임 실행
                        
                        break;
                    case Constants.MultiplayManagerState.EndGame:
                        Debug.Log("## End Game");
                        break;
                }
            });
            
            // Game Logic 객체 생성
            var gameLogic = new GameLogic(blockController, _gameType);
        }
        
        _canvas = GameObject.FindObjectOfType<Canvas>();
    }
}