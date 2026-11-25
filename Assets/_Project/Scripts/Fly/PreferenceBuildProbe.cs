#if UNITY_EDITOR || (PREFERENCE_VERIFICATION && !UNITY_ANDROID)
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FruitFlyParenting.Fly
{
    // Included only in the temporary verification scene/build, never in normal player assemblies.
    [DefaultExecutionOrder(100)]
    public sealed class PreferenceBuildProbe : MonoBehaviour
    {
        [Serializable] sealed class Result
        {
            public bool passed, editor, development, available, automaticallyAttached, visible;
            public string text;
        }
        IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-preferenceVerificationPath");
            if(index<0 || index+1>=args.Length) yield break;
            string path=args[index+1];
            yield return null;
            var fly=FindAnyObjectByType<FlyAgent>();
            var hud=fly.GetComponent<DebugHud>(); bool automatic=hud!=null;
            if(hud==null) hud=fly.gameObject.AddComponent<DebugHud>();
            hud.SetVisible(true);
            yield return new WaitForSecondsRealtime(1f);
            var result=new Result { editor=Application.isEditor,development=Debug.isDebugBuild,
                available=DebugHud.IsAvailable,automaticallyAttached=automatic,visible=hud.Visible,text=hud.DisplayText };
            result.passed=!result.editor && result.available==result.development && automatic==result.development &&
                result.visible==result.development && (!result.development || result.text.Contains("RunSeed:"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,JsonUtility.ToJson(result,true));
            if(result.development)
            {
                var camera=Camera.main; var target=new RenderTexture(1600,1000,24);
                var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false); var old=RenderTexture.active;
                camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply();
                File.WriteAllBytes(Path.ChangeExtension(path,"png"),pixels.EncodeToPNG());
                camera.targetTexture=null; RenderTexture.active=old; target.Release(); Destroy(target); Destroy(pixels);
            }
            Application.Quit(result.passed?0:1);
        }
    }
}
#endif
