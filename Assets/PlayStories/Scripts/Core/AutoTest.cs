using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlayStories
{
    /// <summary>
    /// Automated walkthrough used for visual QA on different aspect ratios.
    /// Run a build with:  PlayStories.exe -autotest "C:\out\folder" -screen-width 1560 -screen-height 720 -screen-fullscreen 0
    /// It clicks through every screen, saves screenshots and quits.
    /// </summary>
    public class AutoTest : MonoBehaviour
    {
        string _out;
        int _shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-autotest");
            if (i < 0) return;
            var go = new GameObject("AutoTest");
            var t = go.AddComponent<AutoTest>();
            t._out = i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autotest");
            Directory.CreateDirectory(t._out);
        }

        IEnumerator Start()
        {
            var app = AppController.Instance;
            var startLanguage = Localization.Current;
            Localization.Current = Language.Danish;
            Log($"screen {Screen.width}x{Screen.height} safe {Screen.safeArea}");
            yield return new WaitForSeconds(2.6f);
            yield return Shot("welcome");

            (app.Current as WelcomeScreen)?.Open();
            yield return new WaitForSeconds(0.45f);
            yield return Shot("welcome_opening");
            yield return new WaitForSeconds(1.8f);
            yield return Shot("library");

            var farm = app.library.books.Find(b => b != null && b.IsPlayable);
            app.OpenBook(farm, null);
            yield return new WaitForSeconds(0.4f);
            yield return Shot("book_opening");
            yield return new WaitForSeconds(2.4f);
            yield return Shot("cover");

            var book = app.Current as BookScreen;
            if (book == null) { Log("no book screen"); Application.Quit(); yield break; }

            // finger drag: a short drag that is released early must snap back to the same page...
            yield return SimulateDrag(book, 0.18f, false);
            yield return new WaitForSeconds(1f);
            Log($"after cancelled drag: page {book.PageIndex} (expected 0)");
            // ...and a long drag turns the page
            yield return SimulateDrag(book, 0.6f, true);
            yield return new WaitForSeconds(2f);
            Log($"after completed drag: page {book.PageIndex} (expected 1)");
            yield return Shot("after_drag");
            book.Prev();
            yield return new WaitForSeconds(2f);
            Log($"after prev: page {book.PageIndex} (expected 0)");

            bool first = true;
            while (book.PageIndex < book.PageCount - 1)
            {
                book.Next();
                if (first)
                {
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("curl_a");
                    yield return new WaitForSeconds(0.25f);
                    yield return Shot("curl_b");
                    first = false;
                }
                yield return new WaitForSeconds(2.4f);
                yield return Shot($"page{book.PageIndex}");
                book.TapCharacter();
                yield return new WaitForSeconds(0.45f);
                yield return Shot($"page{book.PageIndex}_tap");
                yield return new WaitForSeconds(1.6f);
            }
            yield return new WaitForSeconds(1.5f);
            yield return Shot("end");

            app.OpenSettings();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("settings_danish");
            SettingsPanel.CloseIfOpen();
            yield return new WaitForSeconds(0.5f);

            // switch language: the open book is rebuilt in English on the same page
            Localization.Current = Language.English;
            yield return new WaitForSeconds(2.5f);
            yield return Shot("english_end");
            book = app.Current as BookScreen;
            book.Prev();
            yield return new WaitForSeconds(2.4f);
            book.TapCharacter();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("english_duck_tap");

            app.OpenSettings();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("settings_english");
            Localization.Current = startLanguage;
            Log("done");
            Application.Quit();
        }

        IEnumerator SimulateDrag(BookScreen book, float distance, bool shoot)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            e.pressPosition = new Vector2(Screen.width * 0.85f, Screen.height * 0.4f);
            e.position = e.pressPosition + new Vector2(-30f, 0f);
            book.OnBeginDrag(e);
            int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                e.position = e.pressPosition - new Vector2(Screen.width * distance * i / steps, -i * 2f);
                book.OnDrag(e);
                yield return new WaitForSeconds(0.025f);
            }
            if (shoot) yield return Shot("drag_held");
            yield return new WaitForSeconds(0.3f); // finger rests: velocity decays
            book.OnDrag(e);
            book.OnEndDrag(e);
        }

        IEnumerator Shot(string name)
        {
            string path = Path.Combine(_out, $"{_shot++:00}_{name}.png");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            Log("shot " + path);
        }

        void Log(string msg)
        {
            Debug.Log("[AutoTest] " + msg);
            File.AppendAllText(Path.Combine(_out, "log.txt"), msg + "\n");
        }
    }
}
