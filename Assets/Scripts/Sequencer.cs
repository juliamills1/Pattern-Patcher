using PathCreation;
using System.Linq;
using UnityEngine;

//-----------------------------------------------------------------------------
// name: Sequencer.cs
// desc: edit and play music sequence from Chuck; update graphics accordingly
//-----------------------------------------------------------------------------

public class Sequencer : MonoBehaviour
{
    //---------- PUBLIC ------------
    public GameObject seqPrefab;
    public Texture t1, t2, t3, t4;
    public PathCreator track1, track2, track3, track4;
    PathCreator[] tracks;

    //--------- GRAPHICS -----------
    // number of slots
    static int cols = 16;
    static int rows = 4;

    // array for slot positions
    Vector3[,] grid;
    // array for slot rotations
    Quaternion[,] gridRotation;
    // array of patch objects
    GameObject[,] seq;

    Color[] pitchColor;
    Texture[] patterns;

    // total number of patches
    private int patches = 0;
    private bool layerOn = true;
    private int bkgMode = 0;

    // last patch selected by mouse
    private int[] lastTarget = {-1, -1};
    // previous patch played
    private int previousSlot = -1;


    //--------- CHUNITY SYNCING -------------
    private ChuckFloatSyncer ckPlayheadPos;
    private ChuckIntSyncer ckCurrent;
    private float[,] seqGain;
    private int[,] seqPattern;
    private int[,] seqPitch;

    void Start()
    {
        InitGraphics();
        InitAudio();
        InitSequence();
    }

    void InitGraphics()
    {
        tracks = new PathCreator[] {track1, track2, track3, track4};
        grid = new Vector3[rows,cols];
        gridRotation = new Quaternion[rows,cols];
        seq = new GameObject[rows,cols];

        // create underlying grid
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                float inc = (float) j / cols;
                grid[i,j] = tracks[i].path.GetPointAtTime(inc, EndOfPathInstruction.Stop);
                gridRotation[i,j] = tracks[i].path.GetRotation(inc);
            }
        }

        Color red = new Color(118, 0, 0, 1);
        Color orange = new Color(220, 88, 40, 1);
        Color yellow = new Color(255, 255, 81, 1);
        Color green = new Color(17, 109, 81, 1);
        Color blue = new Color(22, 88, 235, 1);
        Color purp = new Color(128, 0, 128, 1);
        Color pink = new Color(255, 155, 190, 1);
        pitchColor = new Color[] {red, orange, yellow, green, blue, purp, pink};

        patterns = new Texture[] {t1, t2, t3, t4};
    }

    void InitAudio()
    {
        // start Chuck code
        GetComponent<ChuckSubInstance>().RunFile("sequencing.ck", true);

        // synchronize playhead position
        ckPlayheadPos = gameObject.AddComponent<ChuckFloatSyncer>();
        ckPlayheadPos.SyncFloat(GetComponent<ChuckSubInstance>(), "playheadPos");

        // synchronize current slot number
        ckCurrent = gameObject.AddComponent<ChuckIntSyncer>();
        ckCurrent.SyncInt(GetComponent<ChuckSubInstance>(), "currentSlot");
    }

    // initialize in gain, pattern, and pitch arrays with default values
    void InitSequence()
    {
        seqGain = new float[rows,cols];
        seqPattern = new int[rows,cols];
        seqPitch = new int[rows,cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                seqGain[i,j] = 1.0f;
                seqPattern[i,j] = 0;
                seqPitch[i,j] = 0;
            }
        }
    }

    void Update()
    {
        // on click
        if (Input.GetMouseButtonUp(0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float rayLength = 100.0f;
            if (Physics.Raycast(ray, out hit, rayLength))
            {
                ProcessClick(hit.point);
            }
        }
        else if (Input.GetKeyDown("c"))
        {
            AdjustPitch(0);
        }
        else if (Input.GetKeyDown("d"))
        {
            AdjustPitch(1);
        }
        else if (Input.GetKeyDown("e"))
        {
            AdjustPitch(2);
        }
        else if (Input.GetKeyDown("f"))
        {
            AdjustPitch(3);
        }
        else if (Input.GetKeyDown("g"))
        {
            AdjustPitch(4);
        }
        else if (Input.GetKeyDown("a"))
        {
            AdjustPitch(5);
        }
        else if (Input.GetKeyDown("b"))
        {
            AdjustPitch(6);
        }
        else if (Input.GetKeyDown("1"))
        {
            AdjustPattern(0);
        }
        else if (Input.GetKeyDown("2"))
        {
            AdjustPattern(1);
        }
        else if (Input.GetKeyDown("3"))
        {
            AdjustPattern(2);
        }
        else if (Input.GetKeyDown("4"))
        {
            AdjustPattern(3);
        }
        else if (Input.GetKeyDown("backspace"))
        {
            DeletePatch();
        }
        else if (Input.GetKeyDown("l"))
        {
            ToggleLayer();
        }
        else if (Input.GetKeyDown("i"))
        {
            bkgMode++;
            if (bkgMode > 2)
            {
                bkgMode = 0;
            }
        }

        // update background colour
        ChangeBackgroundMode(bkgMode);

        int currentSlot = ckCurrent.GetCurrentValue();
        float playhead = ckPlayheadPos.GetCurrentValue();

        // glow and fade patches at current playhead position, unless selected
        for (int i = 0; i < rows; i++)
        {
            int[] currentPatch = {i, currentSlot};
            if (!lastTarget.SequenceEqual(currentPatch))
            {
                FadeEmission(i, (int) playhead, playhead);
            }
        }

        // once in a new slot
        if (currentSlot != previousSlot)
        {
            // after first ever note
            if (previousSlot >= 0)
            {
                // turn off emission of past patches, unless selected
                for (int i = 0; i < rows; i++)
                {
                    int[] now = {i, previousSlot};
                    if (!lastTarget.SequenceEqual(now))
                    {
                        ToggleEmission(i, previousSlot, 0);
                    }
                }
            }

            // turn on emission of patches at current playhead position
            for (int i = 0; i < rows; i++)
            {
                ToggleEmission(i, currentSlot, 1);
            }

            previousSlot = currentSlot;
        }
    }

    // what to do when mouse clicked
    void ProcessClick(Vector3 pos)
    {
        int[] target = GetClosest(pos);
        int row = target[0];
        int slot = target[1];

        // if there is already a note in the slot
        if (HasPatch(row, slot))
        {
            // if this is the first click
            if (!lastTarget.SequenceEqual(target))
            {
                SelectPatch(row, slot);

                // if another cube was previously selected
                if (lastTarget[1] >= 0)
                {
                    Deselect(lastTarget[0], lastTarget[1]);
                }
                lastTarget = target;
            }
            else
            {
                Deselect(row, slot);
            }
        }
        else
        {
            CreatePatch(row, slot);
        }
    }

    // add a patch to the sequencer grid
    void CreatePatch(int row, int slot)
    {
        Vector3 gridPos = grid[row,slot];
        Quaternion rotation = gridRotation[row,slot];
        seq[row,slot] = Instantiate(seqPrefab, gridPos, rotation);

        // in case patch was previously deleted
        seqGain[row, slot] = 1.0f;

        UpdateChuck(row, slot);
        patches++;
        UpdateLayer();
    }

    // remove a patch from the sequencer grid
    void DeletePatch()
    {
        // if a patch is selected
        if (lastTarget[1] >= 0)
        {
            int row = lastTarget[0];
            int slot = lastTarget[1];
            GameObject g = seq[row, slot];
            Destroy(g);

            // update game object, gain, pattern, and pitch arrays
            seq[row, slot] = null;
            seqGain[row, slot] = 0.0f;
            seqPattern[row, slot] = 0;
            seqPitch[row, slot] = 0;

            Deselect(row, slot);
            UpdateChuck(row, slot);
            patches--;
            UpdateLayer();
        }
    }

    // select a patch for editing
    void SelectPatch(int row, int slot)
    {
        // turn on emission property
        ToggleEmission(row, slot, 1);

        // set emission color to full value (brightness)
        GameObject g = seq[row, slot];
        Material mat = g.GetComponent<Renderer>().material;
        Color.RGBToHSV(mat.color, out float h, out float s , out float v);
        Color colorFunc = Color.HSVToRGB(h, s, 1.0f);
        mat.SetColor("_EmissionColor", colorFunc);
    }

    // deselect current patch selection
    void Deselect(int row, int slot)
    {
        // turn off emission property
        ToggleEmission(row, slot, 0);

        // reset lastTarget array
        lastTarget[0] = -1;
        lastTarget[1] = -1;
    }

    // change pitch to index provided (where C = 0)
    void AdjustPitch(int i)
    {
        // if a patch is selected
        if (lastTarget[1] >= 0)
        {
            int row = lastTarget[0];
            int slot = lastTarget[1];
            GameObject g = seq[row, slot];
            Material mat = g.GetComponent<Renderer>().material;

            // swap out current hue for new pitch's hue; preserve saturation
            float h, h1, s, s1, v, v1;
            Color.RGBToHSV(mat.color, out h, out s , out v);
            Color.RGBToHSV(pitchColor[i], out h1, out s1, out v1);
            Color colorFunc = Color.HSVToRGB(h1, s, 1.0f);
            mat.SetColor("_BaseColor", colorFunc);
            mat.SetColor("_EmissionColor", colorFunc);

            // update pitch array and send edit to Chuck
            seqPitch[row, slot] = i;
            UpdateChuck(row, slot);
        }
    }

    // change pattern (sample) to index provided
    void AdjustPattern(int i)
    {
        // if a patch is selected
        if (lastTarget[1] >= 0)
        {
            int row = lastTarget[0];
            int slot = lastTarget[1];
            GameObject g = seq[row, slot];
            Material mat = g.GetComponent<Renderer>().material;

            // change texture accordingly
            mat.SetTexture("_BaseMap", patterns[i]);

            // update pattern array and send edit to Chuck
            seqPattern[row, slot] = i;
            UpdateChuck(row, slot);
        }
    }

    // calculate nearest slot in grid
    int[] GetClosest(Vector3 myPos)
    {
        int[] nearest = {0, 0};
        float closestDistSqr = Mathf.Infinity;

        for (int i = 0; i < rows; i++)
        {
            // find slot closest to input
            for (int j = 0; j < cols; j++)
            {
                Vector3 dir = grid[i,j] - myPos;
                float dSqrToTarget = dir.sqrMagnitude;
                if(dSqrToTarget < closestDistSqr)
                {
                    closestDistSqr = dSqrToTarget;
                    nearest[0] = i;
                    nearest[1] = j;
                }
            }
        }
        return nearest;
    }

    // check if the slot has a patch in it
    bool HasPatch(int row, int slot)
    {
        bool full = true;
        if(seq[row, slot] == null)
        {
            full = false;
        }

        return full;
    }

    // turn patch emission on and off
    void ToggleEmission(int row, int slot, int i)
    {
        if (HasPatch(row, slot))
        {
            GameObject g = seq[row, slot];
            Material mat = g.GetComponent<Renderer>().material;

            if (i == 1)
            {
                mat.EnableKeyword("_EMISSION");

            }
            else
            {
                mat.DisableKeyword("_EMISSION");
            }
        }
    }

    // fade emission according to playhead position through the beat
    void FadeEmission(int row, int slot, float playhead)
    {
        if (HasPatch(row, slot))
        {
            float value = 1.0f;

            if (playhead != 0.0f)
            {
                // emission brightness = 1 at start of beat, 0 at end of beat
                value = Mathf.InverseLerp(1.0f, 0.0f, (playhead % 1));
            }

            GameObject g = seq[row, slot];
            Material mat = g.GetComponent<Renderer>().material;
            Color.RGBToHSV(mat.color, out float h, out float s , out float v);
            Color colorFunc = Color.HSVToRGB(h, s, value);
            mat.SetColor("_EmissionColor", colorFunc);
        }
    }

    // send sequencer info to Chuck variables
    void UpdateChuck(int row, int slot)
    {
        GetComponent<ChuckSubInstance>().SetInt("editRow", row);
        GetComponent<ChuckSubInstance>().SetInt("editWhich", slot);
        GetComponent<ChuckSubInstance>().SetInt("editFolder", seqPattern[row, slot]);
        GetComponent<ChuckSubInstance>().SetInt("editPitch", seqPitch[row, slot]);
        GetComponent<ChuckSubInstance>().SetFloat("editGain", seqGain[row, slot]);
        GetComponent<ChuckSubInstance>().BroadcastEvent("editHappened");
    }

    // turn on background layer of audio and corresponding graphics
    void UpdateLayer()
    {
        GetComponent<ChuckSubInstance>().SetInt("patches", patches);

        GameObject ps = GameObject.Find("ParticleSystem");
        ParticleSystem sys = ps.GetComponent<ParticleSystem>();
        var emission = sys.emission;

        if (layerOn)
        {
            // start audio buffer at 9 patches; then send edits when patches added
            // TO DO: why isn't gain 0 when patches all deleted?
            if (patches == 9)
            {
                GetComponent<ChuckSubInstance>().BroadcastEvent("startLayer");
                GetComponent<ChuckSubInstance>().BroadcastEvent("editLayer");
            }
            else if (patches > 9)
            {
                GetComponent<ChuckSubInstance>().BroadcastEvent("editLayer");
            }

            // scale particle system emission rate by number of patches
            if (patches < 8)
            {
                emission.rateOverTime = 0;
            }
            else if (patches == 8)
            {
                emission.rateOverTime = 15;
            }
            else if (patches > 8)
            {
                emission.rateOverTime = 0.4f * Mathf.Pow(patches, 2);
            }
        }
    }

    // turn background audio layer and graphics on/off
    void ToggleLayer()
    {
        GameObject ps = GameObject.Find("ParticleSystem");
        ParticleSystem sys = ps.GetComponent<ParticleSystem>();
        var emission = sys.emission;

        // if on
        if (layerOn)
        {
            // turn off gain in Chuck and particle system emissions
            GetComponent<ChuckSubInstance>().SetInt("patches", 0);
            GetComponent<ChuckSubInstance>().BroadcastEvent("editLayer");
            layerOn = false;
            emission.rateOverTime = 0;
        }
        else
        {
            // turn on
            GetComponent<ChuckSubInstance>().SetInt("patches", patches);
            GetComponent<ChuckSubInstance>().BroadcastEvent("editLayer");
            layerOn = true;

            // re-scale particle emission rate to current number of patches
            if (patches == 8)
            {
                emission.rateOverTime = 15;
            }
            else if (patches > 8)
            {
                emission.rateOverTime = 0.4f * Mathf.Pow(patches, 2);
            }
        }
    }

    // change background colour according to number of patches
    void UpdateBackground()
    {
        Camera cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;

        // find current and previous colour targets
        float lerp = Mathf.InverseLerp(0, 64, patches);
        float previousLerp = Mathf.InverseLerp(0, 64, patches-1);
        Color targetColor = new Color(lerp, lerp, lerp, 1);
        Color previousColor = new Color(previousLerp, previousLerp, previousLerp, 1);

        // unless target background colour has been reached
        if (cam.backgroundColor != targetColor)
        {
            cam.backgroundColor = Color.Lerp(previousColor,
                                             targetColor,
                                             Mathf.PingPong(Time.time, 1));

            // to ensure lerp isn't dumb
            if (Mathf.PingPong(Time.time, 1) > 0.95)
            {
                cam.backgroundColor = targetColor;
            }
        }
    }

    // toggle between different background colours
    void ChangeBackgroundMode(int mode)
    {
        Camera cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;

        // MODES: 0 = default (change with # of patches); 1 = white; 2 = black
        if (mode == 0)
        {
            UpdateBackground();
        }
        else if (mode == 1)
        {
            cam.backgroundColor = Color.white;
        }
        else
        {
            cam.backgroundColor = Color.black;
        }
    }
}