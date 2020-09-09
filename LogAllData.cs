using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


// Responsible for generating 3 data logs (Camera data, Defect data, Selection data)
public class LogAllData : MonoBehaviour {

    public string TestSubjectID;
    public string RunNumber;
    private string DefectDataLogPath;
    private string PerFrameLogPath;
    private string SelectionLogPath;

    private StreamWriter log_writer;
    private StreamWriter defect_data_writer;
    private StreamWriter selection_log_writer;

    public Transform DefectRoot;
    public Transform SelectionRoot;

    private DefectType[] defects;
    private Transform camera;
    private float timestamp;

    void Awake() {
        DefectDataLogPath = "Assets/0_TestLogs/" + TestSubjectID + RunNumber + "_DefectData.txt";   // Logs data for each defect spawned
        PerFrameLogPath = "Assets/0_TestLogs/" + TestSubjectID + RunNumber + "_Log.txt";
        SelectionLogPath = "Assets/0_TestLogs/" + TestSubjectID + RunNumber + "_Selections.txt";
        camera = Camera.main.transform;
        timestamp = 0.0f;

        defect_data_writer = new StreamWriter(DefectDataLogPath, false);
        log_writer = new StreamWriter(PerFrameLogPath, false);
        selection_log_writer = new StreamWriter(SelectionLogPath, false);

        // Header for logs
        log_writer.WriteLine("<Camera Position>:<Camera Rotation>");
        selection_log_writer.WriteLine("<Action>,<Location>:<Nearest Defect>:<Viewing Angle to Nearest Defect (Degrees)");
    }

    void Update() {
        timestamp += Time.deltaTime * 1000;
        string line = timestamp + ":" + camera.position.ToString() + ":" + camera.rotation.ToString();
        //Debug.Log(line);
        log_writer.WriteLine(line);
        log_writer.Flush();
    }

    public void LogDefectMetaData(string augment_type, float transparency) {
        string line = "Presenting defects at " + (100.0f - transparency * 100.0f) +
            "% transparency with " + augment_type + " augmentation";
        defect_data_writer.WriteLine(line);

        // Write header line
        defect_data_writer.WriteLine("<Name>,<Plane Name>,<World Position>:NotMarked, NotRealDefect:<Name of Nearest Neighbor>");
    }

    public void LogDefectPlaneData(Transform plane) {
        string line = "Defects for plane " + plane.name; 
        defect_data_writer.WriteLine(line);
    }

    public void LogDefectData(DefectType defect, DefectType nearest) {
        string line = defect.transform.name + "," + defect.ParentSurface.name + "," + defect.transform.position.ToString() +
            ":" + defect.NotMarked + "," + defect.NotRealDefect + ":" + nearest.transform.name;
        defect_data_writer.WriteLine(line);
    }

    public void LogDefectClose() {
        defect_data_writer.Close();
    }

    public void LogSelection(Vector3 location, bool action) {
        // action == T: selection ~ action == F: deselection
        string act = (action ? "Selection" : "Deselection");

        // Get nearest defect
        DefectType nearest = null;
        float min_dist = float.MaxValue;
        foreach (DefectType def in defects) {
            if (def != null) {
                float dist = Vector3.Distance(def.transform.position, location);
                if (dist < min_dist) {
                    min_dist = dist;
                    nearest = def;
                }
            }
        }

        // Get viewing angle -- angle from defect to camera such that 
        // viewing head-on would produce an angle of 0 degrees, from the defect's left: -90 degrees,
        // and from the defect's right: 90 degrees.
        Vector3 dlv = nearest.transform.forward;
        Vector3 dfv = new Vector3(dlv.z, dlv.y, -dlv.x);
        Debug.Log("Camera Forward: " + Camera.main.transform.forward.ToString());
        Debug.Log("Defect Forward: " + dlv.ToString());
        float viewing_angle = Vector3.Angle(Camera.main.transform.forward, dlv);
        Debug.Log("Viewing Angle: " + viewing_angle);

        string line = timestamp + ":" + act + "," + location + ":" + nearest.name + ":" + viewing_angle;
        selection_log_writer.WriteLine(line);
        selection_log_writer.Flush();
    }

    public void UpdateDefects(GameObject defect_parent) {
        defects = defect_parent.GetComponentsInChildren<DefectType>();
    }
}
