using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.UI;

public class HairSim : MonoBehaviour
{
	//  Config
    [SerializeField] int _numHairs = 200;
    [SerializeField] int _nodesPerHair = 200;
    [SerializeField] float _gravity = 01f;
    [SerializeField] float windForce = 0.5f;
    [Range(0.0001f,0.0005f)] float _simSpeed = 0.0004f; 

	// Hair nodes
    static public int nHairs = 200;
    static public int nodesPerHair = 50;

    static float nodeStepSize = 5;

    // Gravity
    static public float gravityForce = 0.1f;
    static public float simulationSpeed = 0.0004f;
    static public float forceStrength = 5.0f;

    // Output
    static RenderTexture renderTexture;
    static GameObject mainCanvas;
    static Image outputImage;

    // Hair Nodes
    static HairNode[] hairNodesArray;
    static ComputeBuffer hairNodesBuffer;

    // Circle Colliders
    static circleCollider[] circleCollidersArray;
    static ComputeBuffer circleCollidersBuffer;
    static ComputeBuffer visBuffer;
    static GameObject[] circleColliderObjects;

    // Pivot
    static float[] debugArray;
    static ComputeBuffer debugBuffer;
    static float[] pivotPosition;
    static float[] pivotActualArray;
    static ComputeBuffer pivotActualBuffer;

    static Vector2 oldMouseButton;
    static int nCircleColliders;
    static int colliderUnderControlIndex;

    // Shader
    static ComputeShader _shader;

    // Shader Kernel Interaction (KI)
    static int kiVelShare;
    static int kiCalc;
    static int kiCalcApply;
    static int kiVisInternodeLines;
    static int kiPixelsToTexture;
    static int kiClearPixels;
    static int kiClearTexture;
    static int kiOneThreadAction;
    static int kiInteractionWithColliders;


    void initTexture()
	{
        Debug.Log("Init Texture");
        renderTexture = new RenderTexture(1024, 1024, 32);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
	}
    void initCanvas()
	{
        Debug.Log("Init Canvas");
        // Canvas Scaling
        mainCanvas = GameObject.Find("canvas");
        mainCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceCamera;
        mainCanvas.GetComponent<Canvas>().worldCamera = Camera.main;
        mainCanvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        mainCanvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(Screen.width, Screen.height);
        mainCanvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1.0f;

        // Output image
        outputImage = GameObject.Find("canvas/image").GetComponent<Image>();
        outputImage.color = new Color(1, 1, 1, 1);
        outputImage.material.mainTexture = renderTexture;
        outputImage.type = Image.Type.Simple;
	}

    void initData()
	{
        Debug.Log("Init Data");
        
        nHairs = _numHairs;
		nodesPerHair = _nodesPerHair;
		nodeStepSize = 5;
		simulationSpeed = _simSpeed;
		gravityForce = _gravity;

        int i, hairIndex, nodeIndex;
        hairNodesArray = new HairNode[nHairs * nodesPerHair];

        i = 0;

        while (i < hairNodesArray.Length) {
			hairIndex = i / nodesPerHair;
			nodeIndex = i % nodesPerHair;
			hairNodesArray[i].x = hairIndex - nHairs / 2;
			hairNodesArray[i].y = -nodeStepSize * (nodeIndex - nodesPerHair / 2);
			hairNodesArray[i].vx = 0;
			hairNodesArray[i].vy = 0;
			hairNodesArray[i].dvx = 0;
			hairNodesArray[i].dvy = 0;
			i++;
		}
        
		circleColliderObjects = GameObject.FindGameObjectsWithTag("circleCollider");
		circleCollidersArray = new circleCollider[circleColliderObjects.Length];
		nCircleColliders = circleColliderObjects.Length;
		i = 0;
		while (i < circleColliderObjects.Length)
        {
			circleCollidersArray[i].x = circleColliderObjects[i].transform.position.x;
			circleCollidersArray[i].y = circleColliderObjects[i].transform.position.y;
			circleCollidersArray[i].r = circleColliderObjects[i].transform.localScale.x * circleColliderObjects[i].GetComponent<CircleCollider2D>().radius;
			i++;
		}
		circleCollidersBuffer = new ComputeBuffer(circleCollidersArray.Length, 4 * 8);

		debugArray = new float[128];
		debugBuffer = new ComputeBuffer(debugArray.Length, 4);

		pivotActualArray = new float[2];
		pivotActualArray[0] = 0;
		pivotActualArray[1] = nodeStepSize * nodesPerHair / 2;
		pivotActualBuffer = new ComputeBuffer(1, 8);
		pivotActualBuffer.SetData(pivotActualArray);
    }

    static void putCollidersDataToArray()
	{
        int i = 0;

        while (i < circleColliderObjects.Length)
        {
            circleCollidersArray[i].x = circleColliderObjects[i].transform.position.x;
            circleCollidersArray[i].y = circleColliderObjects[i].transform.position.y;
            circleCollidersArray[i].r = circleColliderObjects[i].transform.localScale.x * circleColliderObjects[i].GetComponent<CircleCollider2D>().radius;
            circleCollidersArray[i].dvx = 0;
            circleCollidersArray[i].dvy = 0;
            i++;
        }
    }

    void initBuffers()
	{
        Debug.Log("Init Buffers");
        hairNodesBuffer = new ComputeBuffer(hairNodesArray.Length, 4 * 8);
        hairNodesBuffer.SetData(hairNodesArray);
        visBuffer = new ComputeBuffer(1024 * 1024, 4);
	}

    void initShader()
	{
        Debug.Log("Init Shader");
        pivotPosition = new float[2];
        pivotPosition[0] = 0;
        pivotPosition[1] = nodeStepSize * nodesPerHair / 2;
        _shader = Resources.Load<ComputeShader>("HairShader"); // Shader must be in assets/resources

        // Assign initial values to shader HairNode struct
        _shader.SetInt("nNodesPerHair", nodesPerHair);
		_shader.SetInt("nHairs", nHairs);
		_shader.SetInt("nCircleColliders", circleCollidersArray.Length);
		_shader.SetFloat("internodeDistance", nodeStepSize);
		_shader.SetFloats("pivotDestination", pivotPosition);
		_shader.SetFloat("dPosRate", simulationSpeed);
		_shader.SetFloat("dVelRate", forceStrength);
		_shader.SetFloat("gravityForce", gravityForce);
        _shader.SetFloat("windForce", windForce);
        _shader.SetFloat("time", Time.timeSinceLevelLoad);
		_shader.SetInt("ftoi", 2 << 17);
		_shader.SetFloat("itof", 1f/(2 << 17));
		_shader.SetInt("nCircleColliders", nCircleColliders);

        // Kernel Assignment
		kiCalc = _shader.FindKernel("calc");
		_shader.SetBuffer(kiCalc, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiCalc, "debugBuffer", debugBuffer);

		kiVelShare = _shader.FindKernel("velShare");
		_shader.SetBuffer(kiVelShare, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiVelShare, "debugBuffer", debugBuffer);

		kiInteractionWithColliders = _shader.FindKernel("interactionWithColliders");
		_shader.SetBuffer(kiInteractionWithColliders, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiInteractionWithColliders, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiInteractionWithColliders, "circleCollidersBuffer", circleCollidersBuffer);

		kiCalcApply = _shader.FindKernel("calcApply");
		_shader.SetBuffer(kiCalcApply, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiCalcApply, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiCalcApply, "pivotActual", pivotActualBuffer);

		kiVisInternodeLines = _shader.FindKernel("visInternodeLines");
		_shader.SetBuffer(kiVisInternodeLines, "hairNodesBuffer", hairNodesBuffer);
		_shader.SetBuffer(kiVisInternodeLines, "visBuffer", visBuffer);

		kiPixelsToTexture = _shader.FindKernel("pixelsToTexture");
		_shader.SetTexture(kiPixelsToTexture, "renderTexture", renderTexture);
		_shader.SetBuffer(kiPixelsToTexture, "visBuffer", visBuffer);

		kiClearPixels = _shader.FindKernel("clearPixels");
		_shader.SetBuffer(kiClearPixels, "visBuffer", visBuffer);

		kiClearTexture = _shader.FindKernel("clearTexture");
		_shader.SetTexture(kiClearTexture, "renderTexture", renderTexture);

		kiOneThreadAction = _shader.FindKernel("oneThreadAction");
		_shader.SetBuffer(kiOneThreadAction, "debugBuffer", debugBuffer);
		_shader.SetBuffer(kiOneThreadAction, "pivotActual", pivotActualBuffer);
	}

    void staticInit()
	{
        initTexture();
        initCanvas();
        initData();
        initBuffers();
        initShader();

	}

    // Controls
    Vector2 getOldMouseButton()
	{
        return Camera.main.ScreenToWorldPoint(Input.mousePosition);
    }
    void HandleInput()
	{
        Vector2 mousePosDelta, rbPos;

        if (Input.GetMouseButtonDown(0))
		{
            oldMouseButton = getOldMouseButton();
		}

        if (Input.GetMouseButtonDown(1))
		{
            oldMouseButton = getOldMouseButton();
            colliderUnderControlIndex = 0;
            circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().velocity = Vector2.zero;
            circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        if (Input.GetMouseButtonUp(1))
		{
            circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
		}

        // Left Mouse Hold
        if (Input.GetMouseButton(0))
		{
            mousePosDelta = getOldMouseButton() - oldMouseButton;
            oldMouseButton = getOldMouseButton();
            pivotPosition[0] += mousePosDelta.x;
            pivotPosition[1] += mousePosDelta.y;
		}

        // Right Mouse Hold
        if (Input.GetMouseButton(1))
		{
            mousePosDelta = getOldMouseButton() - oldMouseButton;
            oldMouseButton = getOldMouseButton();
            rbPos = circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().position;
            rbPos += mousePosDelta;
            rbPos = circleColliderObjects[colliderUnderControlIndex].GetComponent<Rigidbody2D>().position = rbPos;

        }
	}

    // Shader
    void HandleShader()
	{
		int i, nHairThreadGroups, nNodesThreadGroups;
		nHairThreadGroups = (nHairs - 1) / 16 + 1;
		nNodesThreadGroups = (nodesPerHair - 1) / 8 + 1;
		_shader.SetFloats("pivotDestination", pivotPosition);
        _shader.SetFloat("time", Time.timeSinceLevelLoad);
        _shader.SetFloat("windForce", Random.Range(0.100f, 0.200f));
        
		circleCollidersBuffer.SetData(circleCollidersArray);
		i = 0;
		while (i < 40) {
			_shader.Dispatch(kiVelShare, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiCalc, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiInteractionWithColliders, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiCalcApply, nHairThreadGroups, nNodesThreadGroups, 1);
			_shader.Dispatch(kiOneThreadAction, 1, 1, 1);
			i++;
		}
		circleCollidersBuffer.GetData(circleCollidersArray);
		_shader.Dispatch(kiVisInternodeLines, nHairThreadGroups, nNodesThreadGroups, 1);
		_shader.Dispatch(kiClearTexture, 32, 32, 1);
		_shader.Dispatch(kiPixelsToTexture, 32, 32, 1);
		_shader.Dispatch(kiClearPixels, 32, 32, 1);
        //debug
		//debugBuffer.GetData(debugArray);
		//Debug.Log(debugArray[0] + " " + debugArray[1] + "     " + debugArray[2] + " " + debugArray[3] + "     " + debugArray[4] + " " + debugArray[5] + "     " + debugArray[6] + " " + debugArray[7] + "     " + debugArray[8] + " " + debugArray[9] + "     " + debugArray[10] + " " + debugArray[11]);
	}

    // Start is called before the first frame update
    void Start()
    {
        staticInit();
    }

    // Update is called once per frame
    void Update()
    {
        HandleShader();
        HandleInput();
    }

    void FixedUpdate()
    {
        int i;
		Vector2 dv;
		//use colliders data
		i = 0;
		while (i < circleCollidersArray.Length) {
			dv = 0.0000006f * new Vector2(circleCollidersArray[i].dvx, circleCollidersArray[i].dvy);
			circleColliderObjects[i].GetComponent<Rigidbody2D>().AddForce(dv);
			i++;
		}
		putCollidersDataToArray();
    }
	void OnDestroy()
	{
        hairNodesBuffer.Release();
        visBuffer.Release();
        circleCollidersBuffer.Release();
        debugBuffer.Release();
        pivotActualBuffer.Release();
	}
}
