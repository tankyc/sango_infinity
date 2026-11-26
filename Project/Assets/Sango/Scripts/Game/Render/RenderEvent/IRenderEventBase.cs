using Sango.Core;
using System.Collections.Generic;

namespace Sango.Render
{
    public interface IRenderEventBase
    {
        void LockDepends();
        void ReleaseDepends();

        void AddChild(IRenderEventBase child);

        bool IsInited { get; set; }
        bool IsDone { get; set; }

        bool IsVisible();
        bool UpdateInternal(Scenario scenario, float deltaTime);
        bool Update(Scenario scenario, float deltaTime);
        
        void Enter(Scenario scenario);
        void Exit(Scenario scenario);
    }
}
