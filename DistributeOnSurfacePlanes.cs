using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Distributes defects across bridge surface planes
public class DistributeOnSurfacePlanes : MonoBehaviour {
    
    public enum AUGMENT_TYPE {
        Square = 0,
        Corners,
        Outline,
        NoOutline,
    };

    private readonly float[] transparency_distribution_target = { 0.22f, 0.22f, 0.22f, 0.22f };
    private readonly float[] transparency_values = { 0.25f, 0.5f, 0.75f, 1.0f };

    private readonly float[] marked_distribution_target = { 0.40f, 0.40f, 0.20f };

    private float DEFECT_W;
    private float DEFECT_H;

    public GameObject PlaneCollection;
    public AUGMENT_TYPE OutlineType;
    public int MaxDefectsPerPlane;
    public float run_transparency;

    private int[] transparency_uses;
    private int[] marked_uses;
    private float[] plane_sizes;
    private float max_plane_size;
    private DefectType[] defects;
    private GameObject root;
    private MeshFilter[] surf_planes;
    private List<List<Plane>> defects_by_plane;
    private Sprite augment;
    private LogAllData logger;

    // Use this for initialization
    void Start () {
        logger = GameObject.Find("Logger").GetComponent<LogAllData>();
        defects = gameObject.GetComponentsInChildren<DefectType>();
        surf_planes = PlaneCollection.GetComponentsInChildren<MeshFilter>();
        plane_sizes = new float[surf_planes.Length];
        defects_by_plane = new List<List<Plane>>();
        //defect_mats = Resources.LoadAll<Material>("");
        root = defects[0].gameObject;
        transparency_uses = new int[]{ 1, 1, 1, 1 };
        marked_uses = new int[]{ 0, 0, 0 };

        // Assign user-selected augmentation type
        string aug_path = "demoseed_box";
        switch (OutlineType) {
            case AUGMENT_TYPE.Square:
                aug_path = "demoseed_box";
                break;
            case AUGMENT_TYPE.Corners:
                aug_path = "demoseed_box_corner";
                break;
            case AUGMENT_TYPE.Outline:
                aug_path = "demoseed_box_squiggly";
                break;
            default:
                aug_path = "demoseed_box";
                break;
        }
        augment = Resources.Load<Sprite>(aug_path);
        Debug.Log("Sprite to use: " + augment.ToString());
        
        Bounds def_bounds = root.transform.Find("demoseed_augmentation").GetComponent<SpriteRenderer>().bounds;
        Vector3 min_d = def_bounds.min;
        Vector3 max_d = def_bounds.max;
        DEFECT_W = Mathf.Abs(max_d.z - min_d.z);
        DEFECT_H = Mathf.Abs(max_d.y - min_d.y);
        Debug.Log("Width, Height: " + DEFECT_W + ", " + DEFECT_H);

        for (int i = 0; i < surf_planes.Length; i++) {
            defects_by_plane.Add(new List<Plane>());
            Vector3 min = surf_planes[i].mesh.bounds.min;
            Vector3 max = surf_planes[i].mesh.bounds.max;
            plane_sizes[i] = Mathf.Abs((max.x - min.x) * (max.y - min.y));
        }

        max_plane_size = Mathf.Max(plane_sizes);

        // Distribute defects randomly among planes
        MeshFilter surf = surf_planes[0];
        int surf_idx = 0;
        int num_planes_to_use = (int) (surf_planes.Length / Random.Range(2.5f, 3.4f));
        List<int> used_surfs = new List<int>();

        int[,] used_defects = new int[defects.Length, 4];
        Debug.Log("Using " + num_planes_to_use + " out of " + surf_planes.Length + "surface planes");

        List<Transform> defects_on_this_plane;
        int def_id = 0;
        logger.LogDefectMetaData(augment.name, run_transparency);
        for (int i = 0; i < num_planes_to_use; i++) {


            defects_on_this_plane = new List<Transform>();
            // Get next random surface to use and mark it as used
            surf_idx = Random.Range(0, surf_planes.Length);
            while (used_surfs.Contains(surf_idx)) {
                surf_idx = Random.Range(0, surf_planes.Length);
            }
            used_surfs.Add(surf_idx);
            surf = surf_planes[surf_idx];
            //logger.LogDefectPlaneData(surf.transform);

            // Assign random number of defects within user-set limit
            int num_defects = Random.Range(1, (int) (plane_sizes[surf_idx] / max_plane_size) * (MaxDefectsPerPlane + 1));

            //Debug.Log("Using plane: " + surf.name + " with " + num_defects + " defects.");
            // Get placement bounds for this surface plane
            Vector3 min = surf.mesh.bounds.min;
            Vector3 max = surf.mesh.bounds.max;
            float rmin_x = Mathf.Min(min.x, max.x) * surf.transform.localScale.x;
            float rmax_x = Mathf.Max(min.x, max.x) * surf.transform.localScale.x;
            float rmin_y = Mathf.Min(min.z, max.z) * surf.transform.localScale.z;
            float rmax_y = Mathf.Max(min.z, max.z) * surf.transform.localScale.z;
            float xrange = (rmax_x - rmin_x - DEFECT_W) / 2.0f;
            float yrange = (rmax_y - rmin_y - DEFECT_H) / 2.0f;

            // Distribute defects across plane
            for (int j = 0; j < num_defects; ++j) {
                //DefectType defect = defects[Random.Range(0, defects.Length)];
                DefectType defect = Instantiate(root).GetComponent<DefectType>();
                defect.ParentSurface = surf.transform;
                defect.transform.parent = transform;
                Material defect_mat = null;

                // Get defect component material so we can set transparency according to target distribution
                defect_mat = defect.transform.Find("demoseed_defect").GetComponent<SpriteRenderer>().material;
                Color new_transparency = defect_mat.color;
                //new_transparency.a = DistributeTransparency(transparency_uses);
                new_transparency.a = run_transparency;
                defect_mat.color = new_transparency;

                // Assign augment sprite
                defect.transform.Find("demoseed_augmentation").GetComponent<SpriteRenderer>().sprite = augment;

                // Set random z-rotation (0, 90, 180, 270)
                // and match x/y-rotations to plane
                float ex = surf.transform.eulerAngles.x;
                float ey = surf.transform.eulerAngles.y;
                float ez = surf.transform.eulerAngles.z;
                if (ey > -180f && ey < -250f) {
                    Debug.Log("Flipping defect " + defect.name);
                }
                else {
                    Debug.Log("Surface " + surf.name + " has rotation of " + surf.transform.eulerAngles);
                }
                Quaternion def_rot = Quaternion.Euler(ex + (ez == 180f  ? -90f : 90f), 
                     ey,
                    new float[] { 0f, 90f, 180f, 270f }[Random.Range(0, 4)]);
                defect.transform.rotation = def_rot;

                // Set 'flagged' and 'visible' attributes according to target distribution
                bool[] status = DistributeMarked(marked_uses);
                defect.NotMarked = (OutlineType == AUGMENT_TYPE.NoOutline ? true : status[0]);
                defect.NotRealDefect = status[1];

                // Check for overlap and reassign if necessary
                bool overlap = true;
                int iters = 0;
                while (overlap && iters < 100) {
                    overlap = false;
                    iters++;
                    defect.transform.position = surf.transform.position + surf.transform.rotation *
                        (new Vector3(
                            (Random.Range(-xrange, xrange)),
                            0.02f,
                            (Random.Range(-yrange, yrange))));
                    foreach (Transform def in defects_on_this_plane) {
                        float dist = Vector3.Distance(def.position, defect.transform.position);
                        if (dist < 2f) {
                            overlap |= true;
                        }
                    }
                }



                if (iters >= 100) {
                    Destroy(defect.gameObject);
                } else {
                    defect.transform.rotation = def_rot;
                    defect.transform.name = "Defect_" + def_id;
                    def_id++;
                    defects_on_this_plane.Add(defect.transform);
                }
            }
        }

        // Log all defect data then close log
        Destroy(root);
        defects = gameObject.GetComponentsInChildren<DefectType>();
        foreach (DefectType def in defects) {
            DefectType nearest = null;
            float min_dist = float.MaxValue;
            foreach (DefectType def2 in defects) {
                if (!ReferenceEquals(def, def2) && def2 != null && def2.name != "RootDefect(Clone)") {
                    float dist = Vector3.Distance(def.transform.position, def2.transform.position);
                    if (dist < min_dist) {
                        min_dist = dist;
                        nearest = def2;
                    }
                }
            }
            if (def.name != "RootDefect") {
                logger.LogDefectData(def, nearest);
            }
        }
        logger.LogDefectClose();
        logger.UpdateDefects(gameObject);
	}

    void Update() {

    }
    
    private float DistributeTransparency(int[] curr_uses) {

        // First four distributions
        if (curr_uses[0] == 0) {
            curr_uses[0] += 1;
            return transparency_values[0];
        } else if (curr_uses[1] == 0) {
            curr_uses[1] += 1;
            return transparency_values[1];
        } else if (curr_uses[2] == 0) {
            curr_uses[2] += 1;
            return transparency_values[2];
        } else if (curr_uses[3] == 0) {
            curr_uses[3] += 1;
            return transparency_values[3];
        }
        
        int num_defects = 0;
        foreach (int use in curr_uses) {
            num_defects += use;
        }
        float[] distribution_diffs = { 0f, 0f, 0f, 0f };
        for (int i = 0; i < curr_uses.Length; i++) {
            distribution_diffs[i] = transparency_distribution_target[i] - curr_uses[i] / num_defects;
        }

        int idx = System.Array.IndexOf(distribution_diffs, Mathf.Max(distribution_diffs));

        curr_uses[idx] += 1;
        return transparency_values[idx];
    }

    private bool[] DistributeMarked(int[] curr_uses) {

        bool[][] results = new bool[][] { new bool[]{ false, false },
            new bool[]{ false, true },
            new bool[]{ true, false } };

        // First three distributions
        if (curr_uses[0] == 0) {
            curr_uses[0] += 1;
            return results[0];
        } else if (curr_uses[1] == 0) {
            curr_uses[1] += 1;
            return results[1];
        } else if (curr_uses[2] == 0) {
            curr_uses[2] += 1;
            return results[2];
        }
        
        // Get flag type with largest offset and return result
        int num_defects = 0;
        foreach (int use in curr_uses) {
            num_defects += use;
        }
        float[] distribution_diffs = new float[3];
        for (int i = 0; i < curr_uses.Length; i++) {
            distribution_diffs[i] = marked_distribution_target[i] - (float)curr_uses[i] / num_defects;
        }

        int idx = System.Array.IndexOf(distribution_diffs, Mathf.Max(distribution_diffs));

        curr_uses[idx] += 1;
        return results[idx];
    }
}
