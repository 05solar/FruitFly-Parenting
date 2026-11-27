using System.Linq;
using FruitFlyParenting.Corridor;
using FruitFlyParenting.Room;
using UnityEngine;
using UnityEngine.AI;

namespace FruitFlyParenting.Fly
{
    // The asset is a visual child. Navigation, contact colliders and story actors keep their identity.
    public sealed class FlyCharacterArt : MonoBehaviour
    {
        public const string VisualName="Fly Character Art";
        public const float CharacterScale=1.6f;
        public Transform Visual { get; private set; }
        public LifeStage VisualStage { get; private set; }
        FlyAgent actor;
        NavMeshAgent navigation;
        Transform[] wings;
        Renderer[] surfaces;
        Vector3 restPosition;
        AdolescenceCostumeView costume;
        Transform uniform, alternative, flightSignal;
        public float TopY
        {
            get
            {
                float top=transform.position.y;
                if(surfaces!=null)foreach(var r in surfaces)if(r!=null && r.enabled && r.gameObject.activeInHierarchy)top=Mathf.Max(top,r.bounds.max.y);
                return top;
            }
        }
        void Start()
        {
            actor=GetComponent<FlyAgent>();navigation=GetComponent<NavMeshAgent>();
            if(actor!=null)Install(actor.Stage);
        }
        public void Install(LifeStage stage,float floorY=-.28f)
        {
            if(transform.GetComponentsInChildren<Transform>(true).Any(t=>t.name==VisualName))return;
            var prefab=Resources.Load<GameObject>("FlyCharacter/"+stage);
            if(prefab==null)return;
            actor=GetComponent<FlyAgent>();navigation=GetComponent<NavMeshAgent>();
            VisualStage=stage;
            Transform parent=transform;
            string appearance=stage==LifeStage.Childhood ? "Childhood Appearance" : stage==LifeStage.Adolescence ? "Adolescence Appearance" : "Young Adult Appearance";
            var existing=transform.Find(appearance);if(existing!=null)parent=existing;
            foreach(var r in GetComponentsInChildren<MeshRenderer>(true))
            {
                // Smoke, shock cues and ending-specific props still belong to their story controllers.
                bool storyEffect=r.name.Contains("Spark") || HasAncestor(r.transform,"Shock Spark") ||
                    HasAncestor(r.transform,"Smoke Placeholder") || r.name=="Cigarette Placeholder" ||
                    HasAncestor(r.transform,"Star Costume");
                if(!storyEffect)r.enabled=false;
            }
            Visual=Instantiate(prefab,parent,false).transform;Visual.name=VisualName;
            Visual.localScale*=CharacterScale;
            Visual.localPosition=new Vector3(0,floorY,0);restPosition=Visual.localPosition;
            if(stage==LifeStage.MiddleAge)Visual.localRotation=Quaternion.Euler(0,180,0);
            wings=Visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Wing Pivot")).ToArray();
            surfaces=Visual.GetComponentsInChildren<Renderer>(true);
            costume=GetComponent<AdolescenceCostumeView>();
            uniform=Visual.Find("Uniform");alternative=Visual.Find("Alternative");flightSignal=transform.Find("Flight Wings");
        }
        static bool HasAncestor(Transform t,string name)
        { for(var p=t.parent;p!=null;p=p.parent)if(p.name==name)return true;return false; }
        public static void InstallEnding(GameObject owner)
        {
            if(owner==null)return;
            var art=owner.GetComponent<FlyCharacterArt>() ?? owner.AddComponent<FlyCharacterArt>();
            art.Install(LifeStage.MiddleAge,.95f);
        }
        void LateUpdate()
        {
            if(Visual==null || Time.timeScale<=0)return;
            float speed=navigation!=null && navigation.enabled && navigation.isOnNavMesh ? navigation.velocity.magnitude : 0;
            bool flying=flightSignal!=null && flightSignal.gameObject.activeSelf;
            float phase=Time.time*(flying ? 24 : speed>.05f ? 18 : 5);
            for(int i=0;i<wings.Length;i++)wings[i].localRotation=Quaternion.Euler(0,0,(i==0 ? -1:1)*Mathf.Sin(phase)*(flying ? 30 : speed>.05f ? 14 : 4));
            Visual.localPosition=restPosition+Vector3.up*(Mathf.Sin(Time.time*(speed>.05f ? 15:3))*(speed>.05f ? .008f:.003f));
            if(uniform!=null && alternative!=null)
            { bool hidden=costume!=null && costume.Hidden;uniform.gameObject.SetActive(!hidden);alternative.gameObject.SetActive(hidden); }
        }
    }
}
