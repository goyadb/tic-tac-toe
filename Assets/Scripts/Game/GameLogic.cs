
using System;
using UnityEngine;

public abstract class BasePlayerState
{
    public abstract void OnEnter(GameLogic gameLogic);
    public abstract void OnExit(GameLogic gameLogic);
    public abstract void HandleMove(GameLogic gameLogic, int row, int col);
    protected abstract void HandleNextTurn(GameLogic gameLogic);

    protected void ProcessMove(GameLogic gameLogic, Constants.PlayerType playerType, int row, int col)
    {
        if (gameLogic.SetNewBoardValue(playerType, row, col))
        {
            var gameResult = gameLogic.CheckGameResult();

            if (gameResult == GameLogic.GameResult.None)
            {
                HandleNextTurn(gameLogic);
            }
            else
            {
                gameLogic.EndGame(gameResult);
            }
        }
    }
}

// 직접 플레이 (싱글, 네트워크)
public class PlayerState : BasePlayerState
{
    private Constants.PlayerType _playerType;
    private bool _isFirstPlayer;
    
    private MultiplayManager _multiplayManager;
    private string _roomId;
    private bool _isMultiplay;
    
    public PlayerState(bool isFirstPlayer)
    {
        _isFirstPlayer = isFirstPlayer;
        _playerType = _isFirstPlayer ? Constants.PlayerType.PlayerA : Constants.PlayerType.PlayerB;
        _isMultiplay = false;
    }

    public PlayerState(bool isFirstPlayer, MultiplayManager multiplayManager, string roomId)
        : this(isFirstPlayer)
    {
        _multiplayManager = multiplayManager;
        _roomId = roomId;
        _isMultiplay = true;
    }
    
    public override void OnEnter(GameLogic gameLogic)
    {
        gameLogic.blockController.OnBlockClickedDelegate = (row, col) =>
        {
            HandleMove(gameLogic, row, col);
        };
    }

    public override void OnExit(GameLogic gameLogic)
    {
        gameLogic.blockController.OnBlockClickedDelegate = null;
    }

    public override void HandleMove(GameLogic gameLogic, int row, int col)
    {
        ProcessMove(gameLogic, _playerType, row, col);
        if (_isMultiplay)
        {
            _multiplayManager.SendPlayerMove(_roomId, row * Constants.BlockColumnCount + col);
        }
    }

    protected override void HandleNextTurn(GameLogic gameLogic)
    {
        if (_isFirstPlayer)
        {
            gameLogic.SetState(gameLogic.secondPlayerState);
        }
        else
        {
            gameLogic.SetState(gameLogic.firstPlayerState);
        }
    }
}

// AI 플레이
public class AIState : BasePlayerState
{
    public override async void OnEnter(GameLogic gameLogic)
    {
        // AI 연산
        var result = await MinimaxAIController.GetBestMove(gameLogic.GetBoard());
        if (result.HasValue)
        {
            HandleMove(gameLogic, result.Value.row, result.Value.col);
        }
        else
        {
            gameLogic.EndGame(GameLogic.GameResult.Draw);
        }
    }

    public override void OnExit(GameLogic gameLogic)
    {
    }

    public override void HandleMove(GameLogic gameLogic, int row, int col)
    {
        ProcessMove(gameLogic, Constants.PlayerType.PlayerB, row, col);
    }

    protected override void HandleNextTurn(GameLogic gameLogic)
    {
        gameLogic.SetState(gameLogic.firstPlayerState);
    }
}

// 네트워크 플레이
public class MultiplayState : BasePlayerState
{
    private Constants.PlayerType _playerType;
    private bool _isFirstPlayer;
    
    private MultiplayManager _multiplayManager;

    public MultiplayState(bool isFirstPlayer, MultiplayManager multiplayManager)
    {
        _isFirstPlayer = isFirstPlayer;
        _playerType = _isFirstPlayer ? Constants.PlayerType.PlayerA : Constants.PlayerType.PlayerB;
        _multiplayManager = multiplayManager;
    }
    
    public override void OnEnter(GameLogic gameLogic)
    {
        _multiplayManager.OnOpponentMove = moveData =>
        {
            var row = moveData.position / Constants.BlockColumnCount;
            var col = moveData.position % Constants.BlockColumnCount;
            UnityThread.executeInUpdate(() =>
            {
                HandleMove(gameLogic, row, col);                
            });
        };
    }

    public override void OnExit(GameLogic gameLogic)
    {
        _multiplayManager.OnOpponentMove = null;
    }

    public override void HandleMove(GameLogic gameLogic, int row, int col)
    {
        ProcessMove(gameLogic, _playerType, row, col);
    }

    protected override void HandleNextTurn(GameLogic gameLogic)
    {
        if (_isFirstPlayer)
        {
            gameLogic.SetState(gameLogic.secondPlayerState);
        }
        else
        {
            gameLogic.SetState(gameLogic.firstPlayerState);
        }
    }
}

public class GameLogic: IDisposable
{
    public BlockController blockController;
    private Constants.PlayerType[,] _board;
    
    public BasePlayerState firstPlayerState;      // 첫 번째 턴 상태 객체
    public BasePlayerState secondPlayerState;     // 두 번째 턴 상태 객체
    private BasePlayerState _currentPlayerState;    // 현재 턴 상태 객체
    
    private MultiplayManager _multiplayManager;
    private string _roomId;
    
    public enum GameResult
    {
        None,   // 게임 진행 중
        Win,    // 플레이어 승
        Lose,   // 플레이어 패
        Draw    // 비김
    }
    
    public GameLogic(BlockController blockController, 
        Constants.GameType gameType)
    {
        this.blockController = blockController;
        
        // _board 초기화
        _board = new Constants.PlayerType[Constants.BlockColumnCount, Constants.BlockColumnCount];

        switch (gameType)
        {
            case Constants.GameType.SinglePlayer:
            {
                firstPlayerState = new PlayerState(true);
                secondPlayerState = new AIState();

                // 게임 시작
                SetState(firstPlayerState);
                break;
            }
            case Constants.GameType.DualPlayer:
            {
                firstPlayerState = new PlayerState(true);
                secondPlayerState = new PlayerState(false);
                // 게임 시작
                SetState(firstPlayerState);
                break;
            }
            case Constants.GameType.MultiPlayer:
            {
                _multiplayManager = new MultiplayManager((state, roomId) =>
                {
                    _roomId = roomId;
                    switch (state)
                    {
                        case Constants.MultiplayManagerState.CreateRoom:
                            Debug.Log("## Create Room");
                            // TODO: 대기 화면 표시
                            // GameManager에게 대기 화면 표시 요청
                            break;
                        case Constants.MultiplayManagerState.JoinRoom:
                            Debug.Log("## Join Room");
                            firstPlayerState = new MultiplayState(true, _multiplayManager);
                            secondPlayerState = new PlayerState(false, _multiplayManager, roomId);
                            // 게임 시작
                            SetState(firstPlayerState);
                            break;
                        case Constants.MultiplayManagerState.StartGame:
                            Debug.Log("## Start Game");
                            firstPlayerState = new PlayerState(true, _multiplayManager, roomId);
                            secondPlayerState = new MultiplayState(false, _multiplayManager);                            
                            // 게임 시작
                            SetState(firstPlayerState);
                            break;
                        case Constants.MultiplayManagerState.ExitRoom:
                            Debug.Log("## Exit Room");
                            // TODO: Exit Room 처리
                            break;
                        case Constants.MultiplayManagerState.EndGame:
                            Debug.Log("## End Game");
                            // TODO: End Room 처리
                            break;
                    }
                });
                break;
            }
        }
    }

    public void SetState(BasePlayerState state)
    {
        _currentPlayerState?.OnExit(this);
        _currentPlayerState = state;
        _currentPlayerState?.OnEnter(this);
    }
    
    /// <summary>
    /// _board에 새로운 값을 할당하는 함수
    /// </summary>
    /// <param name="playerType">할당하고자 하는 플레이어 타입</param>
    /// <param name="row">Row</param>
    /// <param name="col">Col</param>
    /// <returns>False가 반환되면 할당할 수 없음, True는 할당이 완료됨</returns>
    public bool SetNewBoardValue(Constants.PlayerType playerType, int row, int col)
    {
        if (_board[row, col] != Constants.PlayerType.None) return false;
        
        if (playerType == Constants.PlayerType.PlayerA)
        {
            _board[row, col] = playerType;
            blockController.PlaceMarker(Block.MarkerType.O, row, col);
            return true;
        }
        else if (playerType == Constants.PlayerType.PlayerB)
        {
            _board[row, col] = playerType;
            blockController.PlaceMarker(Block.MarkerType.X, row, col);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 게임 결과 확인 함수
    /// </summary>
    /// <returns>플레이어 기준 게임 결과</returns>
    public GameResult CheckGameResult()
    {
        if (MinimaxAIController.CheckGameWin(Constants.PlayerType.PlayerA, _board)) { return GameResult.Win; }
        if (MinimaxAIController.CheckGameWin(Constants.PlayerType.PlayerB, _board)) { return GameResult.Lose; }
        if (MinimaxAIController.IsAllBlocksPlaced(_board)) { return GameResult.Draw; }
        
        return GameResult.None;
    }
    
    /// <summary>
    /// 게임 오버시 호출되는 함수
    /// gameResult에 따라 결과 출력
    /// </summary>
    /// <param name="gameResult">win, lose, draw</param>
    public void EndGame(GameResult gameResult)
    {
        SetState(null);
        
        firstPlayerState = null;
        secondPlayerState = null;
        
        GameManager.Instance.OpenGameOverPanel();
    }

    public Constants.PlayerType[,] GetBoard()
    {
        return _board;
    }

    public void Dispose()
    {
        _multiplayManager?.LeaveRoom(_roomId);
        _multiplayManager?.Dispose();
    }
}
