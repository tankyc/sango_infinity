using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public class GameState : Singletion<GameState>
    {
        public enum State : int
        {
            None = 0,
            GAME_START_MENU = 1,
            GAMEING = 2,
        }

        int curState = 0;

        public int GetCurState()
        {
            return curState;
        }

        public void ChangeState(int dest)
        {
            if (curState != dest)
            {
                int last = curState;
                Event.OnGameStateExit?.Invoke((int)curState, (int)dest);
                curState = dest;
                Event.OnGameStateEnter?.Invoke((int)curState, (int)last);
            }
        }
    }
}
