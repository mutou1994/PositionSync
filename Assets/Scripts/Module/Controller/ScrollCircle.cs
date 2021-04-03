using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;


public class ScrollCircle : ScrollRect
{
    private float recoveryTime = 0.1f;
    private float recoveryStartTime = 0;
    protected float mRadius;
    public bool isOnEndDrag = false;
    private Vector3 offsetVector3 = Vector3.zero;
    public bool isDrag = false;
    private float lastHVLength = 0;
    public Vector3 mOffsetVector3
    {
        get { return offsetVector3; }
    }

    [System.Serializable] public class OnBeginDragHandler : UnityEvent { }
    [System.Serializable] public class OnEndDragHandler : UnityEvent { }
    [System.Serializable] public class OnDragHandler : UnityEvent<Vector2> { }

    [SerializeField] public OnDragHandler onDrag;
    [SerializeField] public OnBeginDragHandler onBeginDrag;
    [SerializeField] public OnEndDragHandler onEndDrag;


    public GameObject objJianTou;


    protected override void Start()
    {
        inertia = false;
        movementType = MovementType.Unrestricted;
        mRadius = (transform as RectTransform).sizeDelta.x * 0.5f;
        if (objJianTou == null)
            objJianTou = transform.Find("image_JianTou").gameObject;
        objJianTou.transform.localScale = Vector3.zero;
    }


    public void AddOnBeginDragHandler(UnityAction unityAction)
    {
        if (unityAction == null) return;
        onBeginDrag.RemoveAllListeners();
        onBeginDrag.AddListener(unityAction);
    }

    public void AddOnDragHandler(UnityAction<Vector2> unityAction)
    {
        if (unityAction == null) return;
        onDrag.RemoveAllListeners();
        onDrag.AddListener(unityAction);
    }

    public void AddOnEndDragHandler(UnityAction unityAction)
    {
        if (unityAction == null) return;
        onEndDrag.RemoveAllListeners();
        onEndDrag.AddListener(unityAction);
    }


    public override void OnBeginDrag(PointerEventData eventData)
    {
        base.OnBeginDrag(eventData);
        if (onBeginDrag != null)
            onBeginDrag.Invoke();
        //objJianTou.SetActive(true);
        objJianTou.transform.localScale = Vector3.one;
    }
    public override void OnDrag(PointerEventData eventData)
    {
        base.OnDrag(eventData);
        isOnEndDrag = false;
        isDrag = true;
        var contentPostion = this.content.anchoredPosition;
        if (contentPostion.magnitude > mRadius)
        {
            contentPostion = contentPostion.normalized * mRadius;
            SetContentAnchoredPosition(contentPostion);
        }

        if (content.localPosition != Vector3.zero)
        {
            float angle = 0;
            //objJianTou.SetActive(true);
            objJianTou.transform.localScale = Vector3.one;
            Vector3 cross = Vector3.Cross(from, content.transform.localPosition);
            angle = Vector2.Angle(from, content.transform.localPosition);
            angle = cross.z < 0 ? -angle : angle;
            objJianTou.transform.eulerAngles = new Vector3(0, 0, angle - 45);
        }

    }

    public override void OnEndDrag(PointerEventData eventData)
    {
        base.OnEndDrag(eventData);
        if (!isOnEndDrag)
            isOnEndDrag = true;
        isDrag = false;
        if (onEndDrag != null)
            onEndDrag.Invoke();
        recoveryStartTime = Time.realtimeSinceStartup;
        objJianTou.transform.localScale = Vector3.zero;
        objJianTou.transform.eulerAngles = Vector3.zero;
    }


    void Update()
    {
        /*        if (Input.GetMouseButtonDown(0))*/
        {
            UpdateContent();
            if (offsetVector3 != Vector3.zero)
            {
                if (onDrag != null && !isOnEndDrag)
                {
                    onDrag.Invoke(new Vector2(offsetVector3.x, offsetVector3.y));
                }

            }
//#if UNITY_EDITOR
            if (isDrag) return;
            float h = 0, v = 0;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
            {
                h = Input.GetAxis("Horizontal");
            }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow))
            {
                v = Input.GetAxis("Vertical");
            }
            if (h != 0 || v != 0)
            {
                if (lastHVLength == 0 && onBeginDrag != null)
                {
                    onBeginDrag.Invoke();
                }
                Vector2 contentPostion = new Vector2(h, v) * mRadius;
                if (contentPostion.magnitude > mRadius)
                {
                    contentPostion = contentPostion.normalized * mRadius;
                    SetContentAnchoredPosition(contentPostion);
                }
                else
                    SetContentAnchoredPosition(contentPostion);
            }
            else if (!isOnEndDrag && content.localPosition != Vector3.zero)
            {
                isOnEndDrag = true;
                if (onEndDrag != null)
                {
                    onEndDrag.Invoke();
                }
                recoveryStartTime = Time.realtimeSinceStartup;
            }
            lastHVLength = h * h + v * v;
//#endif
        }

    }

    Vector2 from = new Vector2(1, 0);
    private void UpdateContent()
    {
        if (isOnEndDrag)
        {
            float rate = System.Math.Min((Time.realtimeSinceStartup - recoveryStartTime) / recoveryTime, 1);
            float x = Mathf.Lerp(content.localPosition.x, 0.0f, rate);
            float y = Mathf.Lerp(content.localPosition.y, 0.0f, rate);
            content.localPosition = new Vector3(x, y, content.localPosition.z);
            if (content.localPosition == Vector3.zero)
            {
                isOnEndDrag = false;
            }
        }
        CalculateOffset();
    }

    private void CalculateOffset()
    {
        offsetVector3 = content.localPosition / mRadius;
    }


    public Vector3 GetOffsetVector3()
    {
        return offsetVector3;
    }
    public void Remove()
    {
        if (onDrag != null)
        {
            onDrag.RemoveAllListeners();
            onDrag = null;
        }

        if (onBeginDrag != null)
        {
            onBeginDrag.RemoveAllListeners();
            onBeginDrag = null;
        }
        if (onEndDrag != null)
        {
            onEndDrag.RemoveAllListeners();
            onEndDrag = null;
        }
    }
}
