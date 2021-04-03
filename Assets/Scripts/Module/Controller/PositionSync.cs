using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class PositionSync : MonoBehaviour
{
    int index;
    public struct PositionData {
        public Vector3 position;
        public float angle;
        public float time;
        public bool isFinal;
    }

    public enum SyncEnum {
        Force = 1,
        Smooth = 2,
        SmoothDR = 3,
        SmoothDR2 = 4,
        SmoothDR3 = 4,
    }

    public SyncEnum Flag = SyncEnum.Force;
    public ScrollCircle m_FingerTouch;
    public RectTransform m_Scene;
    public RectTransform m_Role;
    public RectTransform m_SyncRole;
    public InputField m_LatencyInput;
    public InputField m_ShakeInput;
    public Dropdown FlagDrop;

    public int m_Speed = 500;
    public float m_PosThreshold = 50;
    public float m_AngleThreshold = 5;
    public float m_PosSyncInterval = 100;
    public float m_MaxReckoningTime = 200;

    private bool m_IsMoving = false;
    private Vector4 m_PosLimit = Vector4.zero;

    private Vector4 m_LastReportPos;
    private float m_LastReportTime;
    private float m_LastLentacy;

    private Vector3 targetPos;
    private float smoothTime;
    private float recvTime;
    private Vector3 startPos;

    bool DRing = false;
    float SmoothLeftDis;
    float SmoothSpeed;
    Vector3 DrDelta;

    PositionData curPosData;
    PositionData lastPosData;
    List<PositionData> positionDatas;

    void Awake()
    {
        m_FingerTouch.AddOnBeginDragHandler(OnMoveBegain);
        m_FingerTouch.AddOnEndDragHandler(OnMoveEnd);
        m_FingerTouch.AddOnDragHandler(OnMoveing);
        Vector2 limit = (m_Scene.sizeDelta - m_Role.sizeDelta) * 0.5f;
        m_PosLimit[0] = -limit.x;
        m_PosLimit[1] = limit.x;
        m_PosLimit[2] = -limit.y;
        m_PosLimit[3] = limit.y;
        m_LastReportPos = m_Role.localPosition;
        m_LastReportTime = Time.realtimeSinceStartup * 1000;
        FlagDrop.value = (int)Flag - 1;
        FlagDrop.onValueChanged.AddListener(v =>
        {
            Flag = (SyncEnum)FlagDrop.value + 1;
        });

        curPosData = new PositionData
        {
            position = m_SyncRole.localPosition,
            angle = m_SyncRole.localEulerAngles.z,
            time = Time.realtimeSinceStartup * 1000,
            isFinal = true,
        };

        positionDatas = new List<PositionData>(10);
        positionDatas.Add(curPosData);
    }
    
    void Update()
    {
        CheckReportPosition();
        SyncPosition();
    }

    void CheckReportPosition()
    {
        float nowTime = Time.realtimeSinceStartup * 1000;
        float dis = Vector3.Distance(m_Role.localPosition, m_LastReportPos);
        float angle = Mathf.Abs(m_Role.localEulerAngles.z - m_LastReportPos[3]);
        bool dirty = dis >= m_PosThreshold || angle >= m_AngleThreshold;
        if (!dirty && nowTime - m_LastReportTime >= m_PosSyncInterval)
        {
            dirty = dis >= 0.01f || angle >= 0.01f; 
        }
        if(dirty)
        {
            StartCoroutine(DelaySend());
        }
    }

    IEnumerator DelaySend() 
    {
        float nowTime = Time.realtimeSinceStartup * 1000;
        PositionData data = new PositionData
        {
            position = m_Role.localPosition,
            angle = m_Role.localEulerAngles.z,
            time = nowTime,
            isFinal = !m_IsMoving,
        };
        float latency = int.Parse(m_LatencyInput.text);
        float shake = int.Parse(m_ShakeInput.text);
        float minLatency = m_LastLentacy - (nowTime - m_LastReportTime);
        if(minLatency <= 0 || minLatency < latency - shake)
        {
            minLatency = latency - shake;
            minLatency = minLatency < 0 ? 0 : minLatency;
        }
        m_LastLentacy = Random.Range(minLatency, latency + shake);
        
        m_LastReportTime = nowTime;
        m_LastReportPos = m_Role.localPosition;
        m_LastReportPos[3] = m_Role.localEulerAngles.z;
        yield return new WaitForSeconds(m_LastLentacy / 1000);
        OnUpdateNetPosition(data);
    }

    void OnMoveBegain()
    {
        m_IsMoving = true;
    }

    void OnMoveEnd()
    {
        //Debug.LogError("MoveEndTime" + (Time.realtimeSinceStartup * 1000));
        m_IsMoving = false;
        StartCoroutine(DelaySend());
    }

    void OnMoveing(Vector2 delta)
    {
        if (!m_IsMoving) return;
        Vector2 pos = m_Role.localPosition;
        pos += delta * m_Speed * Time.deltaTime;
        if(pos.x < m_PosLimit[0]) { pos.x = m_PosLimit[0]; }
        if(pos.x > m_PosLimit[1]) { pos.x = m_PosLimit[1]; }
        if(pos.y < m_PosLimit[2]) { pos.y = m_PosLimit[2]; }
        if(pos.y > m_PosLimit[3]) { pos.y = m_PosLimit[3]; }
        m_Role.localPosition = pos;
        var angle = m_Role.localEulerAngles;
        angle.z = Mathf.Rad2Deg * (Mathf.Atan2(delta.y, delta.x) - Mathf.PI * 0.5f);
        m_Role.localEulerAngles = angle;
    }

    void OnUpdateNetPosition(PositionData positionData)
    {
        if(Flag == SyncEnum.Force)
        {
            SyncPositionForce(positionData);
        }
        else
        {
            if (Flag == SyncEnum.SmoothDR2)
            {
                DRing = false;
                recvTime = Time.realtimeSinceStartup * 1000;
                int count = positionDatas.Count;

                if (!positionDatas[count - 1].isFinal)
                {
                    if(Mathf.Approximately(SmoothLeftDis, DrDelta.magnitude) || SmoothLeftDis < DrDelta.magnitude)
                    {
                        SmoothLeftDis = 0;
                    }
                    else
                    {
                        SmoothLeftDis -= DrDelta.magnitude;
                    }
                }
                
                if (!Mathf.Approximately(SmoothLeftDis, 0) && SmoothLeftDis > 0)
                {
                    SmoothLeftDis += Vector3.Distance(positionData.position, positionDatas[count - 1].position);
                    
                }
                else
                {
                    //TODO 此处可以加一个方向判断 丢弃方向相同 位移相反 后退的点 并且不是最后的终点
                    SmoothLeftDis = Vector3.Distance(positionData.position, m_SyncRole.localPosition);
                }
                smoothTime = recvTime - positionData.time + m_PosSyncInterval;
                if (!positionData.isFinal)
                {
                    DrDelta = (positionData.position - positionDatas[count - 1].position);
                    float deltaTime = m_PosSyncInterval;
                    if (!positionDatas[count - 1].isFinal)
                    {
                        deltaTime = positionData.time - positionDatas[count - 1].time;
                    }
                    float drTime = Mathf.Min(smoothTime, m_MaxReckoningTime);
                    float magnitude = (DrDelta.magnitude * (drTime / deltaTime));
                    DrDelta = DrDelta.normalized * magnitude;
                    SmoothLeftDis += magnitude;
                    smoothTime += drTime;
                }
                else
                {
                    DrDelta = Vector3.zero;
                }
                SmoothSpeed = SmoothLeftDis / (smoothTime / 1000);
                positionDatas.Add(positionData);
            }
            else if(Flag == SyncEnum.SmoothDR3)
            {
                float nowTime = Time.realtimeSinceStartup * 1000;
                float lastRecvTime = recvTime;
                int count = positionDatas.Count;
                if(count >= 2)
                {
                    int index = (int)((nowTime - lastRecvTime) / smoothTime * (count - 1));
                    if(index < count-1)
                    {
                        positionDatas.RemoveRange(0, index + 1);
                    }
                    else
                    {
                        positionDatas.RemoveRange(0, count-1);
                    }
                }
                startPos = m_SyncRole.localPosition;
                recvTime = nowTime;
                smoothTime = nowTime - positionData.time + m_PosSyncInterval;
                if (!positionData.isFinal)
                {
                    float drTime = Mathf.Min(smoothTime, m_MaxReckoningTime);
                    smoothTime += drTime;
                }
                positionDatas.Add(positionData);    
            }
            else 
            { 
                lastPosData = curPosData;
                curPosData = positionData;

                startPos = m_SyncRole.localPosition;
                recvTime = Time.realtimeSinceStartup * 1000;
                float latency = recvTime - positionData.time;
                smoothTime = latency + m_PosSyncInterval;
                targetPos = positionData.position;
                if(!positionData.isFinal && Flag == SyncEnum.SmoothDR)
                {
                    float drTime = Mathf.Min(smoothTime, m_MaxReckoningTime);
                    Vector3 dir = (positionData.position - lastPosData.position);
                    float deltaTime = positionData.time - lastPosData.time;
                    if(lastPosData.isFinal)
                    {
                        deltaTime = m_PosSyncInterval;
                    }
                    var tgtPos = positionData.position + dir.normalized * (dir.magnitude * (drTime / deltaTime));
                    targetPos = tgtPos;
                    smoothTime += drTime;
                }
                else
                {
                    
                }

                /*if (targetPos.x < m_PosLimit[0]) { targetPos.x = m_PosLimit[0]; }
                if (targetPos.x > m_PosLimit[1]) { targetPos.x = m_PosLimit[1]; }
                if (targetPos.y < m_PosLimit[2]) { targetPos.y = m_PosLimit[2]; }
                if (targetPos.y > m_PosLimit[3]) { targetPos.y = m_PosLimit[3]; }*/
            }
            
        }
    }

    void SyncPosition()
    {
        if(Flag == SyncEnum.Smooth)
        {
            SyncPositionSmooth();
        }
        else if(Flag == SyncEnum.SmoothDR)
        {
            SyncPositionSmoothAndDeadReckoning();
        }else if(Flag == SyncEnum.SmoothDR2)
        {
            SyncPositionSmoothDR2();
            
        }else if(Flag == SyncEnum.SmoothDR3)
        {
            SyncPositionSmoothDR3();
        }
    }

    void SyncPositionForce(PositionData posData)
    {
        m_SyncRole.localPosition = posData.position;
        var angle = m_SyncRole.localEulerAngles;
        angle.z = posData.angle;
        m_SyncRole.localEulerAngles = angle;
    }

    void SyncPositionSmooth()
    {
        float nowTime = Time.realtimeSinceStartup * 1000;
        if (nowTime - recvTime >= smoothTime) return;
        float rate = Mathf.Clamp((nowTime - recvTime) / smoothTime, 0, 1);
        m_SyncRole.localPosition = Vector3.Lerp(startPos, targetPos, rate);
        Vector3 angle = m_SyncRole.localEulerAngles;
        angle.z = LerpAngle(lastPosData.angle, curPosData.angle, rate);
        m_SyncRole.localEulerAngles = angle;
    }

    void SyncPositionSmoothAndDeadReckoning()
    {
        float nowTime = Time.realtimeSinceStartup * 1000;
        if (nowTime - recvTime >= smoothTime) return;
        float rate = Mathf.Clamp((nowTime - recvTime) / smoothTime, 0, 1);
        m_SyncRole.localPosition = Vector3.Lerp(startPos, targetPos, rate);
        Vector3 angle = m_SyncRole.localEulerAngles;
        angle.z = LerpAngle(lastPosData.angle, curPosData.angle, rate);
        m_SyncRole.localEulerAngles = angle;
    }

    void SyncPositionSmoothDR3()
    {
        if (positionDatas.Count < 2) return;
        float nowTime = Time.realtimeSinceStartup * 1000;
        float timePass = nowTime - recvTime;
        int count = positionDatas.Count;
        int index;
        float weight;
        if(positionDatas[count-1].isFinal)
        {
            index = (int)(timePass / smoothTime * (count-1));
            weight = timePass / smoothTime * (count-1) - index;
        }
        else
        {
            index = (int)(timePass / smoothTime * count);
            weight = timePass / smoothTime * count - index;
        }
        if (index < count-1)
        {
            var preData = positionDatas[index];
            var nextData = positionDatas[index + 1];
            m_SyncRole.localPosition = Vector3.Lerp(startPos, nextData.position, weight);
            if(weight >= 1)
            {
                startPos = nextData.position;
            }
            Vector3 angle = m_SyncRole.localEulerAngles;
            angle.z = LerpAngle(preData.angle, nextData.angle, weight);
            m_SyncRole.localEulerAngles = angle;
        }
        else
        {
            var lastData = positionDatas[count - 1];
            Vector3 angle = m_SyncRole.localEulerAngles;
            angle.z = lastData.angle;
            m_SyncRole.localEulerAngles = angle;
            if(lastData.isFinal)
            {
                m_SyncRole.localPosition = lastData.position;
                positionDatas.RemoveRange(0, count - 1);
            }
            else
            {
                float drTime = Mathf.Min(recvTime - lastData.time + m_PosSyncInterval, m_MaxReckoningTime);
                Vector3 delta = lastData.position - positionDatas[count - 2].position;
                Vector3 targetPos = lastData.position + delta * drTime / (lastData.time - positionDatas[count-2].time);
                float drWeight = index == count - 1 ? weight : 1;//
                //float drWeight = (timePass - smoothTime) / drTime;
                m_SyncRole.localPosition = Vector3.Lerp(startPos, targetPos, drWeight);
                if(drWeight >= 1)
                {
                    positionDatas.RemoveRange(0, count - 1);
                }
            }
        }
    }

    void SyncPositionSmoothDR2()
    {
        if (SmoothLeftDis <= 0) return;
        int count = positionDatas.Count;
        float delta = Time.deltaTime * SmoothSpeed;
        SmoothLeftDis -= delta;
        if(SmoothLeftDis < 0 || Mathf.Approximately(SmoothLeftDis, 0))
        {
            if(!Mathf.Approximately(SmoothLeftDis, 0))
            {
                delta += SmoothLeftDis;
            }
            SmoothLeftDis = 0;
        }
        Vector3 curPos = m_SyncRole.localPosition;
        if(DRing)
        {
            m_SyncRole.localPosition = curPos + DrDelta.normalized * delta;
        }
        else
        {
            int index = -1;
            for(int i = 0; i < count; i++)
            {
                float dis = Vector3.Distance(positionDatas[i].position, curPos);
                if(delta >= dis || Mathf.Approximately(delta, dis))
                {
                    if(Mathf.Approximately(delta, dis))
                    {
                        delta = 0;
                    }
                    else
                    {
                        delta -= dis;
                    }
                    index = i;
                    curPos = positionDatas[i].position;
                }
                else
                {
                    break;
                }
            }
            if(index < count - 1)
            {
                Vector3 dir = positionDatas[index + 1].position - curPos;
                if(index >= 0)
                {
                    float angle = LerpAngle(positionDatas[index].angle, positionDatas[index + 1].angle, delta / dir.magnitude);
                    SetAngle(angle);
                    positionDatas.RemoveRange(0, index + 1);
                }
                else
                {
                    //一个都没超 角度以自己为准
                }
                m_SyncRole.localPosition = curPos + dir.normalized * delta;
                if(SmoothLeftDis <= 0)
                {
                    SmoothLeftDis += Vector3.Distance(m_SyncRole.localPosition, positionDatas[0].position);
                    for(int i=0;i < positionDatas.Count-1; i ++)
                    {
                        SmoothLeftDis += Vector3.Distance(positionDatas[i].position, positionDatas[i + 1].position);
                    }
                    if(!positionDatas[positionDatas.Count-1].isFinal)
                    {
                        SmoothLeftDis += DrDelta.magnitude;
                    }
                }
            }
            else
            {
                if (!positionDatas[count - 1].isFinal)
                {
                    DRing = true;
                    m_SyncRole.localPosition = curPos + DrDelta.normalized * delta;
                }
                else
                {
                    m_SyncRole.localPosition = positionDatas[count - 1].position;
                    
                }
                SetAngle(positionDatas[count - 1].angle);
                if(count > 1)
                {
                    positionDatas.RemoveRange(0, count-1);
                }
            }
        }
    }

    void SetAngle(float angle)
    {
        Vector3 _angle = m_SyncRole.localEulerAngles;
        _angle.z = angle;
        m_SyncRole.localEulerAngles = _angle;
    }


    float LerpAngle(float src, float tgt, float rate)
    {
        return tgt;
        float offset = tgt - src;
        if (offset < -180)
        {
            offset = offset + 360;
        }
        if(offset > 180)
        {
            offset = offset - 360;
        }
        float ret = src + offset * rate;
        if(ret > 360)
        {
            ret = ret - 360;
        }
        if(ret < 0)
        {
            ret = ret + 360;
        }
        return ret;
    }

}
