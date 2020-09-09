using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class Analysis : MonoBehaviour
{
    #region READ_ONLY
    private const float defectSize = 1.2f;
    private const float selectionDistThresh = 0.5f;
    #endregion

    //public string fileID;
    public float lookingDistance;

    // intersection objects
    public GameObject collisionCone;
    private Vector3 coneTipToPosition;
    private float distanceConeTipToCentroid;
    private CollisionList coneCollisionList;

    #region DATA_TYPES
    // 63 planes
    class Defect {
        public string defectName;
        public string planeName;
        public Vector3 position;
        public bool marked;
        public bool real;
        public string nearestNeighborName;
        public bool isHit;
        public float timeLookedAt;

        public Defect(string inputRow) {
            string[] vals = inputRow.Split(new char[] { ',', ':' });
            this.defectName = vals[0];
            this.planeName = vals[1];
            float px = float.Parse(vals[2].Substring(1, vals[2].Length - 1));
            float py = float.Parse(vals[3].Substring(1, vals[3].Length - 1));
            float pz = float.Parse(vals[4].Substring(1, vals[4].Length - 2));
            this.position = new Vector3(px, py, pz);
            this.marked = vals[5].Equals("False");
            this.real = vals[6].Equals("False");
            this.nearestNeighborName = vals[7];
            this.isHit = false;
            this.timeLookedAt = 0f;
        }

        public void SetHit(bool status) {
            this.isHit = status;
        }

        public void SetNeighbor(string name) {
            this.nearestNeighborName = name;
        }

        public void AddTimeLooked(float time) {
            this.timeLookedAt += time;
        }
    }

    class CamData {
        public float timeStamp;
        public Vector3 position;
        public Quaternion orientation;

        public CamData(string inputRow) {
            string[] vals = inputRow.Split(new char[] { ',', ':' });
            this.timeStamp = float.Parse(vals[0]);
            float px = float.Parse(vals[1].Substring(1, vals[1].Length - 1));
            float py = float.Parse(vals[2].Substring(1, vals[2].Length - 1));
            float pz = float.Parse(vals[3].Substring(1, vals[3].Length - 2));
            this.position = new Vector3(px, py, pz);
            float ox = float.Parse(vals[4].Substring(1, vals[4].Length - 1));
            float oy = float.Parse(vals[5].Substring(1, vals[5].Length - 1));
            float oz = float.Parse(vals[6].Substring(1, vals[6].Length - 1));
            float ow = float.Parse(vals[7].Substring(1, vals[7].Length - 3));
            this.orientation = new Quaternion(ox, oy, oz, ow);
        }
    }

    enum Action {
        SELECTION,
        DESELECTION
    }

    class Selection {
        public float timeStamp;
        public Action action;
        public Vector3 position;
        public string nearestDefectName;
        public float viewingAngleToNearestDefect;
        public bool isFalseAlarm;

        public Selection(string inputRow) {
            string[] vals = inputRow.Split(new char[] { ',', ':' });
            this.timeStamp = float.Parse(vals[0]);
            this.action = (vals[1].Equals("Selection") ? Action.SELECTION : Action.DESELECTION);
            float px = float.Parse(vals[2].Substring(1, vals[2].Length - 1));
            float py = float.Parse(vals[3].Substring(1, vals[3].Length - 1));
            float pz = float.Parse(vals[4].Substring(1, vals[4].Length - 2));
            this.position = new Vector3(px, py, pz);
            this.nearestDefectName = vals[5];
            this.viewingAngleToNearestDefect = float.Parse(vals[6]);
            this.isFalseAlarm = false;
        }

        public void SetSelectedDefect(string dName) {
            this.nearestDefectName = dName;
        }

        public void SetFalseAlarm(bool status) {
            this.isFalseAlarm = status;
        }

        override public string ToString() {
            return String.Format("Selection at {0} of defect {1}", this.position, this.nearestDefectName);
        }
    }

    class FalseAlarm {
        public float timeStamp;
        public Action action;
        public Vector3 position;
        public string nearestDefectName;
        public float viewingAngleToNearestDefect;

        public FalseAlarm(string inputRow) {
            string[] vals = inputRow.Split(new char[] { ',', ':' });
            this.timeStamp = float.Parse(vals[0]);
            this.action = (vals[1].Equals("Selection") ? Action.SELECTION : Action.DESELECTION);
            float px = float.Parse(vals[2].Substring(1, vals[2].Length - 1));
            float py = float.Parse(vals[3].Substring(1, vals[3].Length - 1));
            float pz = float.Parse(vals[4].Substring(1, vals[4].Length - 2));
            this.position = new Vector3(px, py, pz);
            this.nearestDefectName = vals[5];
            this.viewingAngleToNearestDefect = float.Parse(vals[6]);
        }
    }
    #endregion

    private StreamWriter analysisResultWriter;
    private StreamWriter angleWriter;
    private StreamWriter vWriter;
    private StreamWriter jWriter;
    
    private Dictionary<string, Transform> surfacePlanes;

    private int cdIdx;

    // Start is called before the first frame update
    void Awake() {
        // Bundle all data files by participant/run
        List<string> fileIDs = new List<string>();
        Dictionary<string, Dictionary<string, string>> filesBundledByParticipantRun = new Dictionary<string, Dictionary<string, string>>();
        TextAsset[] textFiles = Resources.LoadAll<TextAsset>("");
        foreach (TextAsset tf in textFiles) {
            //Debug.Log("Adding data for " + tf.name);
            string participantRun = tf.name.Split('_')[0];
            string fileName = tf.name.Split('_')[1];
            if (!fileIDs.Contains(participantRun) && !participantRun.Equals("")) {
                fileIDs.Add(participantRun);
                filesBundledByParticipantRun.Add(participantRun, new Dictionary<string, string>());
            }
            if (!participantRun.Equals("")) {
                filesBundledByParticipantRun[participantRun][fileName] = tf.text;
            }
        }
        InitPlanes();

        // Get tip of cone for later placement
        Bounds coneBounds = collisionCone.GetComponent<MeshFilter>().mesh.bounds;
        coneTipToPosition = Vector3.Scale(new Vector3(0f, coneBounds.extents.y, 0f), collisionCone.transform.localScale);
        distanceConeTipToCentroid = coneTipToPosition.magnitude;// * 0.8f;
                                                                //Debug.Log("Cone tip to position: " + coneTipToPosition.ToString() + " Distance: " + distanceConeTipToCentroid);
        coneCollisionList = collisionCone.GetComponent<CollisionList>();

        analysisResultWriter = new StreamWriter("Assets/Results/Analysis.csv", false);

        analysisResultWriter.WriteLine("Participant and Run, Jerk Magnitude, Hits, Misses, False Alarms, Total Targets");
        
        foreach (string fileID in fileIDs) { 


            Dictionary<string, Defect> defects = new Dictionary<string, Defect>();
            List<CamData> camData = new List<CamData>();
            List<Selection> selections = new List<Selection>();
            List<FalseAlarm> falseAlarms = new List<FalseAlarm>();
            List<Vector3> camPositionsAtSelection = new List<Vector3>();

            defects = ParseDefectData(filesBundledByParticipantRun[fileID]["DefectData"]);
            camData = parseLogData(filesBundledByParticipantRun[fileID]["Log"]);
            ParseResults(filesBundledByParticipantRun[fileID]["Results"], defects, selections, falseAlarms, camData, camPositionsAtSelection);

            if (fileID == "p5r5") {
                angleWriter = new StreamWriter("Assets/Results/AngleDeltas_Test.csv");
                vWriter = new StreamWriter("Assets/Results/VDeltas_Test.csv");
                jWriter = new StreamWriter("Assets/Results/jerkRMS_Test.csv");
            } else {
                angleWriter = null;
                vWriter = null;
                jWriter = null;
            }

            // Rectify mistakes from 1st round...
            for (int si = 0; si < selections.Count; si++) {
                selections[si].SetSelectedDefect(GetNearestVisibleDefectAtPosition(selections[si].position, defects));
            }

            // Debug objects
            cdIdx = 0;
            
            GenerateOutputFile(analysisResultWriter, defects, selections, camData, falseAlarms, fileID);
        }

        analysisResultWriter.Close();
        if (vWriter != null) { 
            vWriter.Close();
            jWriter.Close();
        }
    }

    #region PARSE_FUNCTIONS
    // Hardcoded plane data from original project
    private void InitPlanes() {
        surfacePlanes = new Dictionary<string, Transform>();
        Transform planeParent = GameObject.Find("SurfacePlanes").transform;
        for (int i = 0; i < planeParent.childCount; i++) {
            Transform child = planeParent.GetChild(i);
            surfacePlanes[child.name] = planeParent.GetChild(i);
        }
    }

    private Dictionary<string, Defect> ParseDefectData(string inputText) {
        Dictionary<string, Defect> defs = new Dictionary<string, Defect>();
        string dd = inputText;
        string[] dLines = Regex.Split(dd, "\r\n");

        for (int di = 2; di < dLines.Length - 1; di++) { // assuming empty line at end of file
            string dName = dLines[di].Split(new char[] { ',', ':' })[0];
            if (dLines[di].Substring(0, 7).Equals("Defect_")) {
                defs[dName] = new Defect(dLines[di]);
            }
        }

        // Set nearest neighbor (bugs in original file generation)
        foreach (KeyValuePair<string, Defect> d in defs) {
            float minDist = float.MaxValue;
            string nameOfClosest = "";
            foreach (KeyValuePair<string, Defect> d2 in defs) {
                float dist = Vector3.Distance(d.Value.position, d2.Value.position);
                if (dist < minDist && !d.Value.defectName.Equals(d2.Value.defectName)) {
                    minDist = dist;
                    nameOfClosest = d2.Key;
                }
            }
            defs[d.Key].SetNeighbor(nameOfClosest);
        }

        return defs;
    }

    private List<CamData> parseLogData(string inputText) {
        string ld = inputText;
        string[] lLines = Regex.Split(ld, "\r\n");
        List<CamData> cData = new List<CamData>();

        for (int li = 2; li < lLines.Length - 1; li++) { // assuming empty line at end of file
            cData.Add(new CamData(lLines[li]));
        }

        return cData;
    }

    private void ParseResults(string inputText, Dictionary<string, Defect> defs, List<Selection> sels,
                              List<FalseAlarm> fa, List<CamData> cData, List<Vector3> camPosAtSels) {
        string hitHeader = "######### HITS #########";
        string falseAlarmHeader = "##### FALSE ALARMS #####";
        string targetHitHeader = "##### TARGET HITS ######";
        string targetMissHeader = "##### TARGET MISSES ####";
        string[] headers = new string[] { hitHeader, falseAlarmHeader, targetHitHeader, targetMissHeader };

        string rt = inputText;
        string[] rLines = Regex.Split(rt, "\r\n");
        int headerIdx = 0;
        bool skipFormat = false;

        for (int lineIdx = 8; lineIdx < rLines.Length; lineIdx++) {
            string line = rLines[lineIdx];
            if (headers.Contains(line)) {
                //Debug.Log("Found header, skip next line");
                skipFormat = true;
                continue;
            } else if (skipFormat) {
                //Debug.Log("Skipping format line");
                skipFormat = false;
                continue;
            } else if (line.Equals("")) {
                //Debug.Log("Empty line, moving to next header");
                headerIdx += 1;
                continue;
            }
            string[] vals = line.Split(new char[] { ',', ':' });
            switch (headerIdx) {
                case 0:
                    //Debug.Log("Parse 0 -- " + vals[0]);
                    camPosAtSels.Add(GetReleventCamData(float.Parse(vals[0]), out int _, cData).position);
                    sels.Add(new Selection(line));
                    break;
                case 1:
                    //Debug.Log("Parse 1");
                    //fa.Add(new FalseAlarm(line));
                    camPosAtSels.Add(GetReleventCamData(float.Parse(vals[0]), out int _, cData).position);
                    sels.Add(new Selection(line));
                    break;
                case 2:
                    //Debug.Log("Parse 2");
                    //defs[vals[0]].SetHit(true);
                    break;
                case 3:
                    //Debug.Log("Parse 3");
                   // defs[vals[0]].SetHit(false);
                    break;
                default:
                    break;
            }
        }

        for (int si = 0; si < sels.Count; si++) {
            if (Vector3.Distance(sels[si].position, defs[sels[si].nearestDefectName].position) < selectionDistThresh
                && defs[sels[si].nearestDefectName].real) {
                defs[sels[si].nearestDefectName].SetHit(true);
            } else {
                sels[si].SetFalseAlarm(true);
            }
        }
    }
    #endregion

    #region ANALYSIS_WRITE_FUNCTIONS

    // Hits and misses divided over total number of (real) targets
    private void WriteHitsToRealTargetsRatio(StreamWriter resWriter, Dictionary<string, Defect> defs, List<FalseAlarm> fas, List<Selection> sels) {
        //analysisResultWriter.WriteLine("SECTION Hits and Misses Over Targets <Hit ratio, miss ratio>");
        Dictionary<string, Defect> visibleDefs = defs.Where(d => d.Value.real || d.Value.marked).ToDictionary(d => d.Key, d => d.Value);
        int hits = defs.Where(d => d.Value.isHit && d.Value.real).Count();
        int misses = defs.Where(d => !d.Value.isHit && d.Value.real).Count();
        //int falseAlarms = defs.Where(d => d.Value.isHit && !d.Value.real && d.Value.marked).Count();
        int falseAlarms = sels.Where(s => s.isFalseAlarm).Count();
        int visibleTargets = visibleDefs.Count();
        //Debug.Log("Hits: " + hits + " -- Misses: " + misses);
        float hitRatio = (float)hits / (float)defs.Count;
        float missRatio = (float)misses / (float)defs.Count;
        float faRatio = (float)falseAlarms / (float)defs.Count;

        //analysisResultWriter.Write(String.Format("{0}, {1}, {2}", hitRatio, missRatio, faRatio));
        resWriter.Write(String.Format("{0}, {1}, {2}, {3}\n", hits, misses, falseAlarms, visibleDefs.Count));
        resWriter.Flush();
        //analysisResultWriter.WriteLine();
    }

    // Formula defined in spec doc
    private void WriteJerkBetweenSelections(StreamWriter resWriter, List<CamData> cData, List<Selection> sels, string pr) {
        Vector3[] velocities = new Vector3[cData.Count];
        Vector3[] accels = new Vector3[velocities.Length];
        Vector3[] jerks = new Vector3[accels.Length];

        //velocities[velocities.Length - 1] = Vector3.zero;
        for (int ci = 1; ci < cData.Count; ci++) {
            CamData cd1 = cData[ci - 1];
            CamData cd2 = cData[ci];
            Vector3 cd1Euler = cd1.orientation.normalized.eulerAngles * Mathf.Deg2Rad;
            Vector3 cd2Euler = cd2.orientation.normalized.eulerAngles * Mathf.Deg2Rad;
            if (cd2.timeStamp != cd1.timeStamp) {

                if (Mathf.Abs(cd2Euler.x - cd1Euler.x) > Mathf.PI) {
                    if (cd2Euler.x > cd1Euler.x) {
                        cd1Euler = new Vector3(cd1Euler.x + 2f * Mathf.PI, cd1Euler.y, cd1Euler.z);
                    } else {
                        cd2Euler = new Vector3(cd2Euler.x + 2f * Mathf.PI, cd2Euler.y, cd2Euler.z);
                    }
                }
                if (Mathf.Abs(cd2Euler.y - cd1Euler.y) > Mathf.PI) {
                    if (cd2Euler.y > cd1Euler.y) {
                        cd1Euler = new Vector3(cd1Euler.x, cd1Euler.y + 2f * Mathf.PI, cd1Euler.z);
                    } else {
                        cd2Euler = new Vector3(cd2Euler.x, cd2Euler.y + 2f * Mathf.PI, cd2Euler.z);
                    }
                }
                if (Mathf.Abs(cd2Euler.z - cd1Euler.z) > Mathf.PI) {
                    if (cd2Euler.z > cd1Euler.z) {
                        cd1Euler = new Vector3(cd1Euler.x, cd1Euler.y, cd1Euler.z + 2f * Mathf.PI);
                    } else {
                        cd2Euler = new Vector3(cd2Euler.x, cd2Euler.y, cd2Euler.z + 2f * Mathf.PI);
                    }
                }

                if ((Mathf.Abs(cd2Euler.x - cd1Euler.x) > Mathf.PI)
                    || (Mathf.Abs(cd2Euler.y - cd1Euler.y) > Mathf.PI)
                    || (Mathf.Abs(cd2Euler.z - cd1Euler.z) > Mathf.PI)) {
                    Debug.Log(cd2Euler.ToString("F1") + " - " + cd1Euler.ToString("F1"));
                }
                velocities[ci] = cd2Euler / (cd2.timeStamp - cd1.timeStamp) - cd1Euler / (cd2.timeStamp - cd1.timeStamp);
                if (angleWriter != null) {
                    float x = velocities[ci].x;
                    float y = velocities[ci].y;
                    float z = velocities[ci].z;
                    angleWriter.WriteLine(cd2Euler.x.ToString("F5") + ", " + cd2Euler.y.ToString("F5") + ", " + cd2Euler.z.ToString("F5"));
                    vWriter.WriteLine(velocities[ci].x.ToString("F5") + ", " + velocities[ci].y.ToString("F5") + ", " + velocities[ci].z.ToString("F5"));
                }
            }
        }
        accels[accels.Length - 1] = Vector3.zero;
        for (int ci = 1; ci < velocities.Length - 1; ci++) {
            CamData cd1 = cData[ci - 1];
            CamData cd2 = cData[ci];
            Vector3 v1 = velocities[ci - 1];
            Vector3 v2 = velocities[ci];
            if (cd2.timeStamp != cd1.timeStamp) {
                accels[ci] = (v2 - v1) / (cd2.timeStamp - cd1.timeStamp);
            }
        }
        jerks[jerks.Length - 1] = Vector3.zero;
        jerks[jerks.Length - 2] = Vector3.zero;
        for (int ci = 1; ci < cData.Count - 2; ci++) {
            CamData cd1 = cData[ci - 1];
            CamData cd2 = cData[ci];
            Vector3 a1 = accels[ci - 1];
            Vector3 a2 = accels[ci];
            if (cd2.timeStamp != cd1.timeStamp) {
                jerks[ci] = (a2 - a1) / (cd2.timeStamp - cd1.timeStamp);
            }
        }

        float[] angularJerk = new float[jerks.Length];
        angularJerk[angularJerk.Length - 1] = 0f;
        angularJerk[angularJerk.Length - 2] = 0f;

        for (int ji = 0; ji < angularJerk.Length; ji++) {
            CamData cd = cData[ji];
            Vector3 cdEuler = cd.orientation.normalized.eulerAngles;
            float tx = cdEuler.x;
            float ty = cdEuler.y;
            float tz = cdEuler.z;
            float tx1 = velocities[ji].x;
            float ty1 = velocities[ji].y;
            float tz1 = velocities[ji].z;
            float tx2 = accels[ji].x;
            float ty2 = accels[ji].y;
            float tz2 = accels[ji].z;
            float tx3 = jerks[ji].x;
            float ty3 = jerks[ji].y;
            float tz3 = jerks[ji].z;


            angularJerk[ji] = Mathf.Sqrt(
                Mathf.Pow((2f * Mathf.Cos(ty) * ty1 * tx2 + tx1 * (-Mathf.Sin(ty) * (ty1 * ty1) + Mathf.Cos(ty) * ty2)
                + tz1 * (-Mathf.Cos(ty) * (Mathf.Cos(tx) * (tx1 * tx1 + ty1 * ty1) + Mathf.Sin(tx) * tx2)
                + Mathf.Sin(ty) * (2f * Mathf.Sin(tx) * tx1 * ty1 - Mathf.Cos(tx) * ty2))
                - 2f * (Mathf.Cos(ty) * Mathf.Sin(tx) * tx1 + Mathf.Cos(tx) * Mathf.Sin(ty) * ty1) * tz2
                + Mathf.Sin(ty) * tx3 + Mathf.Cos(tx) * Mathf.Cos(ty) * tz3), 2)
                +
                Mathf.Pow((2f * Mathf.Sin(ty) * ty1 * tx2 + tx1 * (Mathf.Cos(ty) * (ty1 * ty1) + Mathf.Sin(ty) * ty2)
                + tz1 * (-Mathf.Sin(ty) * (Mathf.Cos(tx) * (tx1 * tx1 + ty1 * ty1) + Mathf.Sin(tx) * tx2)
                + Mathf.Cos(ty) * (-2f * Mathf.Sin(tx) * tx1 * ty1 + Mathf.Cos(tx) * ty2))
                + 2f * (-Mathf.Sin(tx) * Mathf.Sin(ty) * tx1 + Mathf.Cos(tx) * Mathf.Cos(ty) * ty1) * tz2
                - Mathf.Cos(ty) * tx3 + Mathf.Cos(tx) * Mathf.Sin(ty) * tz3), 2)
                +
                Mathf.Pow((Mathf.Cos(tx) * (tz1 * tx2 + 2f * tx1 * tz2) + ty3 + Mathf.Sin(tx) * (-(tx1 * tx1) * tz1 + tz3)), 2)
            );
        }

        float jerkSum = 0f;
        for (int ji = 1; ji < angularJerk.Length; ji++) {
            float timeStep = cData[ji].timeStamp - cData[ji - 1].timeStamp;
            jerkSum += (angularJerk[ji] * angularJerk[ji]) * timeStep;
            if (angleWriter != null) {
                jWriter.WriteLine(String.Format("{0}", (angularJerk[ji] * angularJerk[ji]) * timeStep));
                jWriter.Flush();
            }
        }

        float jerkRMS = Mathf.Sqrt(jerkSum);
        resWriter.Write(String.Format("{0}, ", jerkRMS));
    }
    #endregion

    #region OUTPUT_FUNCTIONS
    private void GenerateOutputFile(StreamWriter resWriter, Dictionary<string, Defect> defs, List<Selection> sels, List<CamData> cData, List<FalseAlarm> fas, string fileID) {
        //Debug.Log("Generating output file...");
        resWriter.Write(fileID + ", ");
        WriteJerkBetweenSelections(resWriter, cData, sels, fileID);
        WriteHitsToRealTargetsRatio(resWriter, defs, fas, sels);
    }
    #endregion

    #region SUPPORT_FUNCTIONS
    private CamData GetReleventCamData(float timeStamp, out int idx, List<CamData> cData) {
        idx = -1;
        float minDiff = float.MaxValue;
        for (int cdi = 0; cdi < cData.Count; cdi++) {
            float diff = Mathf.Abs(timeStamp - cData[cdi].timeStamp);
            if (diff < minDiff) {
                idx = cdi;
                minDiff = diff;
            }
        }
        return cData[idx];
    }

    public static Vector3 NearestPointOnLine(Vector3 linePnt, Vector3 lineDir, Vector3 pnt) {
        var v = pnt - linePnt;
        var d = Vector3.Dot(v, lineDir.normalized);
        return linePnt + lineDir.normalized * d;
    }

    private string GetNearestVisibleDefectAtPosition(Vector3 pos, Dictionary<string, Defect> defs) {
        string dName = "";

        foreach (KeyValuePair<string, Defect> kv in defs) {
            float dist = float.MaxValue;
            if ((kv.Value.real || kv.Value.marked)) {
                float distToDef = Vector3.Distance(pos, kv.Value.position);
                if (distToDef < dist) {
                    dist = distToDef;
                    dName = kv.Key;
                }
            }
        }

        return dName;
    }
    #endregion
}
