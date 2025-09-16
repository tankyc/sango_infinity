#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System.Text;
using UnityEngine;

public class ServerConsole : MonoBehaviour
{

    private ConsoleTestWindows.ConsoleWindow console = new ConsoleTestWindows.ConsoleWindow();
    private ConsoleTestWindows.ConsoleInput input = new ConsoleTestWindows.ConsoleInput();

    private static bool ishowWindow = false;
    private bool oldWindowState = false;
	//
	// Create console window, register callbacks
	//
	void Awake() 
	{
        ishowWindow = true;
        if (ishowWindow)
        {
            console = new ConsoleTestWindows.ConsoleWindow();
            input = new ConsoleTestWindows.ConsoleInput();
            console.Initialize();
            console.SetTitle("调试窗口");
            input.OnInputText += OnInputText;
            Application.logMessageReceived += HandleLog;
        }
        else
        {
            CloseConsoleWindow();
        }
        oldWindowState = false;

        DontDestroyOnLoad( gameObject );
		Debug.Log( "Console Started" );
	}
 
	//
	// Text has been entered into the console
	// Run it as a console command
	//
	void OnInputText( string obj )
	{
        this.ConsolePrint(obj);
	}

    public static string DecFileName(string str)
    {
        Encoding utf8 = Encoding.GetEncoding("iso-8859-1");
        byte[] btArr = utf8.GetBytes(str);
        return Encoding.UTF8.GetString(btArr);
    }

    public static string UTF8ToGBK(string text)
    {
       // Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            Encoding utf8 = Encoding.UTF8;
            Encoding gbk = Encoding.GetEncoding("gbk");//Encoding.Default ,936
            byte[] temp = utf8.GetBytes(text);
            byte[] temp1 = Encoding.Convert(utf8, gbk, temp);
            string result = gbk.GetString(temp1);
            return result;
        }
        catch
        {
            return null;
        }
    }

    public static string GBKToUTF8(string str)
    {
        //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding utf8;
        Encoding gbk;
        utf8 = Encoding.GetEncoding("utf-8");
        gbk = Encoding.GetEncoding("gbk");
        byte[] gb = gbk.GetBytes(str);
        gb = Encoding.Convert(gbk, utf8, gb);
        return utf8.GetString(gb);
    }

    Encoding utf8 = Encoding.UTF8;
    Encoding defaultCode = Encoding.Unicode;
    //
    // Debug.Log* callback
    //
    void HandleLog( string message, string stackTrace, LogType type )
	{
        if (type == LogType.Warning)
            System.Console.ForegroundColor = System.ConsoleColor.Yellow;
        else if (type == LogType.Log)
            System.Console.ForegroundColor = System.ConsoleColor.White;
        else
            System.Console.ForegroundColor = System.ConsoleColor.Red;
 
		// We're half way through typing something, so clear this line ..
        if (System.Console.CursorLeft != 0)
			input?.ClearLine();

        //byte[] utf8Bytes = utf8.GetBytes(message);
        //// Perform the conversion from one encoding to the other.
        //byte[] defaultBytes = Encoding.Convert(utf8, defaultCode, utf8Bytes);
        //char[] defaultChars = new char[defaultCode.GetCharCount(defaultBytes, 0, defaultBytes.Length)];
        //defaultCode.GetChars(defaultBytes, 0, defaultBytes.Length, defaultChars, 0);
        //string defaultString = new string(defaultChars);

        System.Console.WriteLine(message);
 
		// If we were typing something re-add it.
		input?.RedrawInputLine();
	}

    //
    // Update the input every frame
    // This gets new key input and calls the OnInputText callback
    //
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.BackQuote))
        {
            ishowWindow = !ishowWindow;
            if (ishowWindow)
            {
                console = new ConsoleTestWindows.ConsoleWindow();
                input = new ConsoleTestWindows.ConsoleInput();
                console.Initialize();
                console.SetTitle("调试窗口");
                input.OnInputText += OnInputText;
                Application.logMessageReceived += HandleLog;
            }
            else
            {
                CloseConsoleWindow();
            }
            oldWindowState = ishowWindow;
        }
        // input update
        if (ishowWindow && null != input)
        {
            input.Update();
        }

        if (ishowWindow != oldWindowState && !ishowWindow)
        {
            CloseConsoleWindow();
        }
        oldWindowState = ishowWindow;
    }
 
	//
	// It's important to call console.ShutDown in OnDestroy
	// because compiling will error out in the editor if you don't
	// because we redirected output. This sets it back to normal.
	//
	void OnDestroy()
	{
        CloseConsoleWindow();
    }

    void CloseConsoleWindow()
    {
        if (console != null)
        {
            console.Shutdown();
            console = null;
            input = null;
        }
    }
    // control by other .
    public static void SetIshowWindow(bool flag)
    {   
       ishowWindow = flag;
    }

    public static void ShowConsole()
    {
        GameObject console = new GameObject("console");
        console.AddComponent<ServerConsole>();
        DontDestroyOnLoad(console);
    }
}

public static class ExtendDebugClass
{
    public static void ConsolePrint(this MonoBehaviour mono, string message)
    {
        if (message.Length < 0) return;
        System.Console.WriteLine(message);
    }
}
#endif
