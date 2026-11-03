using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

namespace Mkey
{
    public enum MultilineType { Parallel, NormalPreserveSpacing, NormalNotPreserveSpacing }

    [DisallowMultipleComponent]
    public class EasyTextDeformer : BaseMeshEffect 
    {
        #region serialized fields
        public bool useCurve = true;

        [SerializeField]
        private bool rotateSymbols = false;

        [HideInInspector]
        [SerializeField]
        private bool curveCreated;

        [SerializeField]
        private bool adjustSpacing = true;
        [SerializeField]
        protected float m_Spacing = 0f;
        public float Spacing
        {
            get { return m_Spacing; }
            set
            {
                m_Spacing = value;
                OnChangeSpline();
            }
        }
        #endregion serialized fields

        private List<TextLine> lines;
        private Text text;
        private List<UIVertex> vList;
        private float resolut = 0.1f;
        private Vector3 basePos;

        public List<Vector3> handlesPositions;
     //   [SerializeField]
        private  List<CatmullRommCurve> curves;

        #region properties
        /// <summary>
        /// Return handles count
        /// </summary>
        public int HandlesCount
        {
            get
            {
                if (handlesPositions == null) return 0;
                return handlesPositions.Count;
            }
        }

        public bool Selected { get; set; }
        #endregion properties

        public void OnChangeSpline()
        {
            if (text) text.SetVerticesDirty();
          //  Debug.Log("on change spline");
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            //Debug.Log("tr pos world: " + transform.position);
            //Debug.Log("tr pos local: " + transform.localPosition);
            //Debug.Log("anchored pos: " + GetComponent<RectTransform>().anchoredPosition);

            text = GetComponent<Text>(); //Debug.Log("begin modify mesh");
            if (!text) return;
            vList = new List<UIVertex>();
            vh.GetUIVertexStream(vList); // int i = 0;   vList.ForEach((v) => { print("V" + i + " : " + v.position + "; (UV:" + v.uv0 + ":" + v.uv1 + ":" + v.uv2 + ":" + v.uv3 + ")"); i++; });
            if (!enabled) return;
            ModifyVertices(vList);      // i = 0; vList.ForEach((v) => { print("V" + i + " : " + v.position + "; (UV:" + v.uv0 + ":" + v.uv1 + ":" + v.uv2 + ":" + v.uv3 + ")"); i++; });
            vh.Clear();
            vh.AddUIVertexTriangleStream(vList);
        }

        public void OnDrawGizmos()
        {
            if (Selected && enabled)
            {
                if (HandlesCount < 2 || !useCurve) return;
                Gizmos.color = Color.white;
                curves.ForEach((c)=> { if(c!=null) c.DisplayCatmullRomSplineEL(transform, true, false); });
            }
        }

        #region private methods
        private void ModifyVertices(List<UIVertex> vList)
        {
            if (vList.Count < 6) return;
            bool useSpacing = (adjustSpacing && Spacing != 0);
            if (!useCurve && !useSpacing) return;

            string sText = text.text;
            IList<UILineInfo> lInfoL = text.cachedTextGenerator.lines;

            // create text lines
            lines = new List<TextLine>(lInfoL.Count);
            TextLine tLine;
            VChar vChar;
            int sI = 0;
            int eI = 0;
            int vI = 0;
            for (int i = 0; i < lInfoL.Count; i++)
            {
                sI = lInfoL[i].startCharIdx;
                eI = (i < lInfoL.Count - 1) ? lInfoL[i + 1].startCharIdx : sText.Length;

                tLine = new TextLine(eI - sI);
                for (int j = sI; j < eI; j++)
                {
                    vI = j * 6;
                    if (vI < vList.Count)
                    {
                        vChar = new VChar(vList[vI + 0], vList[vI + 1], vList[vI + 2], vList[vI + 3], vList[vI + 4], vList[vI + 5], sText[j]);
                        if (!vChar.IsZero()) tLine.Add(vChar);
                    }
                }
                if (tLine.CharsCount > 0) lines.Add(tLine);
            }
        
            vList.Clear();// Debug.Log("lines count : "+ lines.Count); lines.ForEach((l)=> { Debug.Log(l.CharsToString()); });

            // Adjust spacing 
            if (useSpacing)
            {
                for (int i = 0; i < lines.Count; i++)
                    lines[i].ModifySpacing(text.alignment, Spacing);
            }

            if (useCurve)
            { 
            CreateInitialHandles();
            curves = new List<CatmullRommCurve>(lines.Count);
            curves.Add(new CatmullRommCurve(handlesPositions, 0.05f));
            float curveAnchor = 0; // anchor basepos to curve 0 - start, 1 -end of curve
                if (lines.Count > 0)
                {
                    switch (text.alignment)
                    {
                        case TextAnchor.UpperLeft:
                            basePos = lines[0].VFBL;
                            break;
                        case TextAnchor.UpperCenter:
                            basePos = lines[0].LineCenter;
                            curveAnchor = 0.5f;
                            break;
                        case TextAnchor.UpperRight:
                            basePos = lines[0].VLBR;
                            curveAnchor = 1;
                            break;
                        case TextAnchor.MiddleLeft:
                            basePos = lines[0].VFBL;
                            break;
                        case TextAnchor.MiddleCenter:
                            basePos = lines[0].LineCenter;
                            curveAnchor = 0.5f;
                            break;
                        case TextAnchor.MiddleRight:
                            basePos = lines[0].VLBR;
                            curveAnchor = 1;
                            break;
                        case TextAnchor.LowerLeft:
                            basePos = lines[0].VFBL;
                            break;
                        case TextAnchor.LowerCenter:
                            basePos = lines[0].LineCenter;
                            curveAnchor = 0.5f;
                            break;
                        case TextAnchor.LowerRight:
                            basePos = lines[0].VLBR;
                            curveAnchor = 1;
                            break;
                        default:
                            break;
                    }
                for (int i = 0; i < lines.Count; i++)
                    lines[i].ModifyAlongCurve(curves[0], text, basePos, curveAnchor, rotateSymbols, i);
                }
            } // Debug.Log("end modify mesh");
            for (int i = 0; i < lines.Count; i++)
                vList.AddRange(lines[i].GetUIVertexList());
        }

        private void CreateInitialHandles()
        {
            if (curveCreated) return;
            Debug.Log("create initial curve");
            if (!text) text = GetComponent<Text>();
            if (!text) return;

            if (lines == null || lines.Count == 0) return;


            Vector3 p0 = lines[0].VFBL;
            Vector3 p3 = lines[0].VLBR;
            Vector3 p1 = p0 + (p3 - p0) / 3f;
            Vector3 p2 = p0 + (p3 - p0) / 3f * 2f;

            Debug.Log(p0 + ":" + p1 + ":" + p2 + ":" + p3);

            Vector3[] points = new Vector3[4] { p0, p1, p2, p3 };
            handlesPositions = new List<Vector3>(points);
            curveCreated = true;
        }
        #endregion private methods

        public void RemovePoint(int selectedIndex)
        {
            Debug.Log("Remove point: " + selectedIndex);
            handlesPositions.RemoveAt(selectedIndex);
        }

        public void AddPoint(int selectedIndex)
        {
            Debug.Log("Add point: " + selectedIndex);
            Vector3 p0 = handlesPositions[selectedIndex];
            Vector3 p1 = handlesPositions[selectedIndex + 1];
            Vector3 pn = (p1 + p0) / 2.0f;
            handlesPositions.Insert(selectedIndex + 1, pn);
        }

        public void SetInitial()
        {
            curveCreated = false;
            CreateInitialHandles();
        }
    }

    [System.Serializable]
    public class CatmullRommCurve
    {
        [SerializeField]
        private List<CMPos> controlPositionsExt;
        private EndTangente leftTangente;
        private EndTangente rightTangente;

        /// <summary>
        /// Return path length
        /// </summary>
        public float LengthCPE
        {
            get
            {
                if (controlPositionsExt == null || controlPositionsExt.Count == 0) return 0;
                return controlPositionsExt[controlPositionsExt.Count - 1].Dist;
            }
        }

        public CatmullRommCurve(List<Vector3> handlesPositions, float resolution)
        {
            if (handlesPositions == null) return;
            int handlesCount = handlesPositions.Count;
            if (handlesCount < 2) return;

            List<Vector3>  controlPositions = new List<Vector3>(handlesPositions.Count + 2); // add 2 hidden positions to fix start and end positions of curve 
            controlPositions.Add(handlesPositions[0]); // hidden duplicate 0 point
            handlesPositions.ForEach((p) => { controlPositions.Add(p); });
            controlPositions.Add(handlesPositions[handlesCount - 1]); // hidden duplicate last point

            // extended position list with resolution 
            int loops = Mathf.FloorToInt(1f / resolution);
            controlPositionsExt = new List<CMPos>((handlesPositions.Count - 1) * (loops-1) + controlPositions.Count); //controlPositionsExt = new List<CMPos>((handlesPositions.Count - 1) * 10 + 1);
            float accDist = 0;
            controlPositionsExt.Add(new CMPos(accDist, controlPositions[0]));
            controlPositionsExt.Add(new CMPos(accDist, controlPositions[1]));
           
            Vector3 lastPos = controlPositions[0];
            Vector3 newPos = lastPos;

            for (int posI = 1; posI < controlPositions.Count - 2; posI++)
            {
                Vector3 p0 = controlPositions[ClampListPos(posI - 1, controlPositions.Count)];
                Vector3 p1 = controlPositions[posI];
                Vector3 p2 = controlPositions[ClampListPos(posI + 1, controlPositions.Count)];
                Vector3 p3 = controlPositions[ClampListPos(posI + 2, controlPositions.Count)];

                //add curve positions 
                for (int ip = 1; ip <= loops; ip++)
                {
                    float t = ip * resolution;
                    Vector3 firstDer;
                    newPos = GetCatmullRomPosition(t, p0, p1, p2, p3, out firstDer);
                    accDist += (newPos - lastPos).magnitude;
                    controlPositionsExt[controlPositionsExt.Count - 1].SetDirToNextPos(newPos);
                    controlPositionsExt.Add(new CMPos(accDist, newPos, Vector3.zero, firstDer));
                    lastPos = newPos;
                }
            }
            controlPositionsExt.Add(new CMPos(accDist, controlPositions[controlPositions.Count - 1]));

            controlPositionsExt[0].SetDirToNextPos(controlPositionsExt[0].PosL + controlPositionsExt[1].DirToNext);
            controlPositionsExt[controlPositionsExt.Count - 1].SetDirToNextPos(controlPositionsExt[controlPositionsExt.Count - 1].PosL + controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);
            controlPositionsExt[controlPositionsExt.Count - 2].SetDirToNextPos(controlPositionsExt[controlPositionsExt.Count - 2].PosL +controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);

            // create tangents

            leftTangente = new EndTangente(controlPositionsExt[0].PosL, -controlPositionsExt[1].DirToNext);

            rightTangente = new EndTangente(controlPositionsExt[controlPositionsExt.Count - 1].PosL,  controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);
        }

        /// <summary>
        /// use ext array of control point with addit start and end point
        /// </summary>
        /// <param name="contrPosExt"></param>
        public CatmullRommCurve(List<Vector3> contrPosExt)
        {
            if (contrPosExt == null) return;
            Debug.Log("Create positions");
            if (contrPosExt.Count < 4) return;

            // extended position list 
            controlPositionsExt = new List<CMPos>(contrPosExt.Count); //controlPositionsExt = new List<CMPos>((handlesPositions.Count - 1) * 10 + 1);
            float accDist = 0;
            controlPositionsExt.Add(new CMPos(0, contrPosExt[0], (contrPosExt[1] - contrPosExt[0])));

            for (int posI = 1; posI < contrPosExt.Count - 1; posI++)
            {
                accDist += (contrPosExt[posI] - contrPosExt[posI - 1]).magnitude;
                controlPositionsExt.Add(new CMPos(accDist, contrPosExt[posI], contrPosExt[posI + 1] - contrPosExt[posI]));
            }

            controlPositionsExt.Add(new CMPos(accDist, contrPosExt[contrPosExt.Count - 1]));

            // create tangents
            leftTangente = new EndTangente(controlPositionsExt[0].PosL, -controlPositionsExt[1].DirToNext);
            rightTangente = new EndTangente(controlPositionsExt[controlPositionsExt.Count - 1].PosL, controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);
        }

        public CatmullRommCurve(List<CMPos> controlPosExt)
        {
            if (controlPosExt == null) return;

            controlPositionsExt = new List<CMPos>(controlPosExt); 

            leftTangente = new EndTangente(controlPositionsExt[0].PosL, -controlPositionsExt[1].DirToNext);
            rightTangente = new EndTangente(controlPositionsExt[controlPositionsExt.Count - 1].PosL, controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);
        }

        public CatmullRommCurve(CatmullRommCurve curve, bool asNormal, float dY)
        {
            int count = curve.controlPositionsExt.Count;
            controlPositionsExt = new List<CMPos>(count);

            if (asNormal)
            {
                Vector3 rotDir = curve.controlPositionsExt[1].DirToNext;
                float accDist = 0;

                Vector3 newPos = curve.controlPositionsExt[0].PosL + new Vector3(-rotDir.y, rotDir.x, 0).normalized * dY;
                controlPositionsExt.Add(new CMPos(accDist, newPos));
                controlPositionsExt.Add(new CMPos(accDist, newPos));
                Vector3 lastPos = newPos; 
             
                for (int i = 2; i < count; i++)
                {
                    rotDir =(i<=count-3) ? curve.controlPositionsExt[i].DirToNext : curve.controlPositionsExt[count-3].DirToNext;
                    newPos = curve.controlPositionsExt[i].PosL + new Vector3(-rotDir.y, rotDir.x, 0).normalized * dY;
                    accDist += (newPos - lastPos).magnitude;
                    controlPositionsExt[controlPositionsExt.Count - 1].SetDirToNextPos(newPos);
                    controlPositionsExt.Add(new CMPos(accDist, newPos));
                    lastPos = newPos;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    controlPositionsExt.Add(new CMPos(curve.controlPositionsExt[i]));
                }
            }

            leftTangente = new EndTangente(controlPositionsExt[0].PosL, -controlPositionsExt[1].DirToNext);
            rightTangente = new EndTangente(controlPositionsExt[controlPositionsExt.Count - 1].PosL, controlPositionsExt[controlPositionsExt.Count - 3].DirToNext);
        }

        private int ClampListPos(int pos, int count)
        {
            if (pos < 0)
            {
                pos = count - 1;
            }

            if (pos > count)
            {
                pos = 1;
            }
            else if (pos > count - 1)
            {
                pos = 0;
            }

            return pos;
        }

        /// <summary>
        /// Returns a position between 4 Vector3 with Catmull-Rom spline algorithm
        /// http://www.iquilezles.org/www/articles/minispline/minispline.htm
        /// http://www.gamedev.ru/code/tip/catmull_rom
        /// </summary>
        /// <param name="t"></param>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns></returns>
        private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            //The coefficients of the cubic polynomial (except the 0.5f * which I added later for performance)
            Vector3 a = 2f * p1;
            Vector3 b = p2 - p0;
            Vector3 c = 2f * (p0 - 2.5f * p1) + 4f * p2 - p3;
            Vector3 d = -p0 + 3f * (p1 - p2) + p3;

            //The cubic polynomial: a + b * t + c * t^2 + d * t^3
            Vector3 pos = 0.5f * (a + t*(b + t*(c + (d * t))));

            return pos;
        }

        /// <summary>
        /// Returns a position between 4 Vector3 with Catmull-Rom spline algorithm
        /// http://www.iquilezles.org/www/articles/minispline/minispline.htm
        /// http://www.gamedev.ru/code/tip/catmull_rom
        /// </summary>
        /// <param name="t"></param>
        /// <param name="p0"></param>
        /// <param name="p3"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns></returns>
        private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, out Vector3 firstDeriv)
        {
            Vector3 a = 2f * p1;
            Vector3 b = p2 - p0;
            Vector3 c = 2f * (p0 - 2.5f * p1) + 4f * p2 - p3;
            Vector3 d = -p0 + 3f * (p1 - p2) + p3;

            //The cubic polynomial: a + b * t + c * t^2 + d * t^3
            Vector3 pos = 0.5f * (a + t * (b + t * (c + (d * t))));

            firstDeriv = 0.5f * (b + t * 2.0f * (c + 1.5f * d * t));
            return pos;
        }

        /// <summary>
        /// Return position on spline at distance
        /// </summary>
        /// <param name="dist"></param>
        /// <returns></returns>
        public Vector3 GetPositionEL(float dist, out Vector3 rotDir)
        {
            float pathLength = LengthCPE;
            if (dist >= 0 && dist <= pathLength)
            {
                int point = 0;
                int numPoints = controlPositionsExt.Count;

               // dist = (dist == pathLength) ? pathLength : Mathf.Repeat(dist, pathLength);

                while (point < controlPositionsExt.Count - 1 && controlPositionsExt[point].Dist <= dist)
                {
                    ++point;
                }

                int p1n = ((point - 1) + numPoints) % numPoints;
                int p2n = point;

                float t = Mathf.InverseLerp(controlPositionsExt[p1n].Dist, controlPositionsExt[p2n].Dist, dist);
                rotDir = controlPositionsExt[p2n].DirToNext;

                int p0n = ((point - 2) + numPoints) % numPoints;
                int p3n = (point + 1) % numPoints;
                p2n = p2n % numPoints;

                Vector3 P0 = controlPositionsExt[p0n].PosL;
                Vector3 P1 = controlPositionsExt[p1n].PosL;
                Vector3 P2 = controlPositionsExt[p2n].PosL;
                Vector3 P3 = controlPositionsExt[p3n].PosL;
                return GetCatmullRomPosition(t, P0, P1, P2, P3);
            }
            else if (dist < 0)
            {
                rotDir = new Vector3(-leftTangente.Dir.x, -leftTangente.Dir.y, 0);
                return leftTangente.StartPosL - leftTangente.Dir.normalized * dist;
            }
            else
            {
                rotDir = rightTangente.Dir;
                return rightTangente.StartPosL + rightTangente.Dir.normalized * (dist - pathLength);
            }
        }

        /// <summary>
        /// Return position on spline at distance
        /// </summary>
        /// <param name="dist"></param>
        /// <returns></returns>
        public Vector3 GetPositionEL(float dist)
        {
            float pathLength = LengthCPE;
            if (dist >= 0 && dist <= pathLength)
            {
                int point = 0;
                int numPoints = controlPositionsExt.Count;


                dist = (dist == pathLength) ? pathLength : Mathf.Repeat(dist, pathLength);

                while (point < controlPositionsExt.Count - 1 && controlPositionsExt[point].Dist <= dist)
                {
                    ++point;
                }

                int p1n = ((point - 1) + numPoints) % numPoints;
                int p2n = point;

                float t = Mathf.InverseLerp(controlPositionsExt[p1n].Dist, controlPositionsExt[p2n].Dist, dist);

                int p0n = ((point - 2) + numPoints) % numPoints;
                int p3n = (point + 1) % numPoints;
                p2n = p2n % numPoints;

                Vector3 P0 = controlPositionsExt[p0n].PosL;
                Vector3 P1 = controlPositionsExt[p1n].PosL;
                Vector3 P2 = controlPositionsExt[p2n].PosL;
                Vector3 P3 = controlPositionsExt[p3n].PosL;
                return GetCatmullRomPosition(t, P0, P1, P2, P3);
            }
            else if (dist < 0)
            {
                return leftTangente.StartPosL - leftTangente.Dir.normalized * dist;
            }
            else
            {
                return rightTangente.StartPosL + rightTangente.Dir.normalized * (dist - pathLength);
            }
        }

        /// <summary>
        /// Return List of position on curve with equal distances inbetween
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public List<Vector3> GetPositionsEL(int count)
        {
            List<Vector3> pos = new List<Vector3>();
            float delta = (LengthCPE) / (count - 1);
            Vector3 rD;
            for (int i = 0; i < count - 1; i++)
            {
                pos.Add(GetPositionEL(i * delta, out rD));
            }
            pos.Add(GetPositionEL(LengthCPE, out rD));
            return pos;
        }

        public void DisplayCatmullRomSplineEL(Transform t, bool showEndTangetns)
        {
            if (controlPositionsExt == null || controlPositionsExt.Count < 3) return;

            for (int i = 1; i < controlPositionsExt.Count; i++)
            {
                Debug.DrawLine(t.TransformPoint(controlPositionsExt[i - 1].PosL), t.TransformPoint(controlPositionsExt[i].PosL));
            }

            if (showEndTangetns)
            {
                Debug.DrawLine(t.TransformPoint(leftTangente.StartPosL), t.TransformPoint(leftTangente.StartPosL + leftTangente.Dir * 5), Color.red);
                Debug.DrawLine(t.TransformPoint(rightTangente.StartPosL), t.TransformPoint(rightTangente.StartPosL + rightTangente.Dir * 5), Color.red);
            }
        }

        public void DisplayCatmullRomSplineEL(Transform t, bool showEndTangetns, bool showLineTangents)
        {
            if (controlPositionsExt == null || controlPositionsExt.Count < 3) return;

            for (int i = 1; i < controlPositionsExt.Count; i++)
            {
                Debug.DrawLine(t.TransformPoint(controlPositionsExt[i - 1].PosL), t.TransformPoint(controlPositionsExt[i].PosL), Color.white);
               if(showLineTangents) Debug.DrawLine(t.TransformPoint(controlPositionsExt[i].PosL), t.TransformPoint(controlPositionsExt[i].PosL + controlPositionsExt[i].FirstDer), Color.blue);
            }

            if (showEndTangetns)
            {
                Debug.DrawLine(t.TransformPoint(leftTangente.StartPosL), t.TransformPoint(leftTangente.StartPosL + leftTangente.Dir * 5), Color.red);
                Debug.DrawLine(t.TransformPoint(rightTangente.StartPosL), t.TransformPoint(rightTangente.StartPosL + rightTangente.Dir * 5), Color.red);
            }
        }

        /// <summary>
        /// offset curve using PolyLine offset class
        /// https://www.stat.auckland.ac.nz/~paul/Reports/VWline/offset-xspline/offset-xspline.html
        /// https://medium.com/@all2one/computing-offset-curves-for-cubic-splines-d3f968e5a2e0
        /// https://sourceforge.net/p/polyclipping/code/540/
        /// https://cran.r-project.org/web/packages/polyclip/polyclip.pdf
        /// </summary>
        /// <returns></returns>
        public CatmullRommCurve OffsetCurve(float dist, Transform tr)
        {
            // V 1.4 features
            //-------------------------------------------------------

            //if (controlPositionsExt == null || controlPositionsExt.Count < 2) return null;
            //List<Vector3> newPoss = new List<Vector3>(controlPositionsExt.Count - 2);
            //Debug.Log("controlPositionsExt.Count: " + controlPositionsExt.Count);
            //for (int i = 1; i < controlPositionsExt.Count - 1; i++)
            //{
            //    newPoss.Add(controlPositionsExt[i].PosL);
            //}
       
            ////1) createp line segments - source pline
            //PolyLine pLine = new PolyLine(newPoss);
            //Debug.Log("pLine.points.Count: " + pLine.points.Count);

            //pLine.Display(tr, Color.green);
            ////2) pline offset
            //OffsetPolyLine opl = new OffsetPolyLine(pLine, dist, tr);
            //PolyLine pLine_1 = opl.GetOffsetPline();
            //if (pLine_1 != null) pLine_1.Display(tr, Color.blue);

            //if (pLine_1 != null && pLine_1.points != null && pLine_1.points.Count > 0)
            //{
            //    List<Vector3> nP = new List<Vector3>();
            //    nP.Add(pLine_1.points[0]);
            //    nP.AddRange(pLine_1.points);
            //    nP.Add(pLine_1.points[pLine_1.points.Count - 1]);
            //    return new CatmullRommCurve(nP);
            //}
            return null;
        }
    }

    [System.Serializable]
    public class CMPos
    {
        [SerializeField]
        private float dist; // distance from first point 
        [SerializeField]
        private Vector3 posL; // local position 
        [SerializeField]
        private Vector3 dirToNext; // direction to next point
        [SerializeField]
        private Vector3 firstDer; // first derivative

        /// <summary>
        /// distance from first point 
        /// </summary>
        public float Dist
        {
            get { return dist; }
        }

        /// <summary>
        /// first derivative
        /// </summary>
        public Vector3 FirstDer
        {
            get { return firstDer; }
        }

        /// <summary>
        /// local position 
        /// </summary>
        public Vector3 PosL
        {
            get { return posL; }
        }

        /// <summary>
        /// direction to next point
        /// </summary>
        public Vector3 DirToNext
        {
            get { return dirToNext; }
        }

        public CMPos(float dist, Vector3 posL)
        {
            this.dist = dist;
            this.posL = posL;
        }

        public CMPos(float dist, Vector3 posL, Vector3 dirToNext)
        {
            this.dist = dist;
            this.posL = posL;
            this.dirToNext = dirToNext;
        }

        public CMPos(float dist, Vector3 posL, Vector3 dir, Vector3 firstDer)
        {
            this.dist = dist;
            this.posL = posL;
            this.dirToNext = dir;
            this.firstDer = firstDer;
        }

        public CMPos(CMPos pos)
        {
            dist = pos.dist;
            posL = pos.posL;
            dirToNext = pos.dirToNext;
        }

        public void SetDirToNextPos(Vector3 nextPos)
        {
            dirToNext = nextPos - PosL;
        }

    }

    [System.Serializable]
    public class EndTangente
    {
        [SerializeField]
        private Vector3 startPosL;
        public Vector3 StartPosL { get { return startPosL;  } private set { startPosL = value; } }

        [SerializeField]
        private Vector3 dir;
        public Vector3 Dir { get { return dir; } private set { dir = value; } }

        public EndTangente(Vector3 startL, Vector3 dir)
        {
            StartPosL = startL;
            Dir = dir;
        }
    }


    // Class used for char vertex deformation
    public class VChar
    {
        public UIVertex[] vertices;

        /// <summary>
        /// Return bootom left vertice (vertices[4].position)
        /// </summary>
        public Vector3 BL
        {
            get { return vertices[4].position; }
        }
        public char lChar;
        public int charCode;

        public VChar(UIVertex v0, UIVertex v1, UIVertex v2, UIVertex v3, UIVertex v4, UIVertex v5, char ch)
        {
            vertices = new UIVertex[] { v0, v1, v2, v3, v4, v5 };
            lChar = ch;
            charCode = (int)lChar;
        }
  
        public Vector3 Center()
        {
            return (vertices[0].position + vertices[3].position) / 2.0f;
        }

        public void Move(Vector3 add)
        {
            vertices[0].position += add;
            vertices[1].position += add;
            vertices[2].position += add;
            vertices[3].position += add;
            vertices[4].position += add;
            vertices[5].position += add;
        }

        public void Rotate(Vector3 dir)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.right, dir);
            Matrix4x4 m = Matrix4x4.Rotate(rotation);
            int i = 0;
            Vector3 dPos4;
            while (i < vertices.Length)
            {
                dPos4 = vertices[i].position - vertices[4].position; // rotate around V4
                vertices[i].position = vertices[4].position + m.MultiplyPoint3x4(dPos4);
                i++;
            }
        }

        public bool IsZero()
        {
            return (vertices[0].position == vertices[3].position);
        }
    }

    // Class used for text lines 
    public class TextLine
    {
        public List<VChar> vChars;

        /// <summary>
        /// First bottom left vertice ( vChars[0].vertices[4].position)
        /// </summary>
        public Vector2 VFBL
        {
            get { return vChars[0].vertices[4].position; }
        }

        /// <summary>
        /// Left symbol  center ( vChars[0].Center())
        /// </summary>
        public Vector2 VFC
        {
            get { return vChars[0].Center(); }
        }

        /// <summary>
        /// Right symbol  center ( vChars[vChars.Count - 1].Center())
        /// </summary>
        public Vector2 VLC
        {
            get { return vChars[vChars.Count - 1].Center(); }
        }

        /// <summary>
        /// Last bottom right vertice (vChars[vChars.Count - 1].vertices[3].position)
        /// </summary>
        public Vector2 VLBR
        {
            get { VChar vC = vChars[vChars.Count - 1]; return vC.vertices[3].position; }
        }

        public Vector2 LineCenter
        {
            get { return new Vector2((VLBR.x + VFBL.x) / 2.0f, (vChars[0].vertices[4].position.y)); }
        }

        public List<UIVertex> GetUIVertexList()
        {
            List<UIVertex> vL = new List<UIVertex>(CharsCount * 6);
            foreach (var item in vChars)
            {
                vL.AddRange(item.vertices);
            }
            return vL;
        }

        public int CharsCount
        {
            get { return (vChars == null) ? 0 : vChars.Count; }
        }

        public TextLine(int capacity)
        {
            vChars = new List<VChar>(capacity);
        }

        /// <summary>
        /// Change letter spacing
        /// </summary>
        /// <param name="textAnchor"></param>
        /// <param name="spacing"></param>
        /// <returns></returns>
        public void ModifySpacing(TextAnchor textAnchor, float spacing)
        {
            float cis = 0;
            VChar vChar;
            float dPos = 0;
            int vCount = vChars.Count * 6;
            if (textAnchor == TextAnchor.LowerLeft || textAnchor == TextAnchor.MiddleLeft || textAnchor == TextAnchor.UpperLeft)
            {
                dPos = 0;
            }
            else if (textAnchor == TextAnchor.LowerCenter || textAnchor == TextAnchor.MiddleCenter || textAnchor == TextAnchor.UpperCenter)
            {
                dPos = (vCount - 6) / 12f * spacing;
            }
            else
            {
                dPos = (vCount - 6) / 6f * spacing;
            }

            for (int i = 0, ci = 0; i < vChars.Count; i++, ci++)
            {
                cis = spacing * ci - dPos;
                vChar = vChars[i];
                vChar.Move(new Vector3(cis, 0, 0));
            }
        }

        /// <summary>
        /// Align text lines along curve
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="text"></param>
        /// <param name="basePos"></param>
        /// <param name="rotate"></param>
        /// <param name="mlType"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public void Modify(CatmullRommCurve curve, Text text, Vector3 basePos, bool rotate, MultilineType mlType, int number)
        {
            VChar vChar;
            Vector3 dPos; // newPos - oldPos - delta char position
            Vector3 newPos;
            Vector3 oldPos;
            Vector3 rotDir;
            float charDistanceOldX = 0;
            float charDistanceNewX = 0;

            if (mlType == MultilineType.NormalNotPreserveSpacing) // normal offset and not preserve letter spacing
            {
                for (int i = 0; i < vChars.Count; i++)
                {
                    vChar = vChars[i];
                    charDistanceOldX = vChar.vertices[4].position.x - basePos.x;  // charDistanceOld = Mathf.Abs(vChar.vertices[4].position.x - basePos.x);
                    charDistanceNewX = charDistanceOldX;// charDistanceNew = charDistanceOld * k;//   Debug.Log(charDistanceOld + " : " + charDistanceNew);
                    float dy = vChar.vertices[4].position.y - basePos.y;

                    oldPos = vChar.vertices[4].position; // oldPos = new Vector3(vChar.vertices[4].position.x, basePos.y, vChar.vertices[4].position.z);
                    newPos = curve.GetPositionEL(charDistanceNewX, out rotDir) + new Vector3(-rotDir.y, rotDir.x, 0).normalized * dy;  // newPos = text.transform.InverseTransformPoint(curve.GetPositionEL(charDistanceNew, out rotDir));//   Debug.Log(oldPos+" : "+newPos);

                    // Debug.Log(charDistanceOld + " : "+oldPos + " : " + newPos);
                    dPos = newPos - oldPos;

                    vChar.Move(dPos);
                    if (rotate) vChar.Rotate(rotDir);
                }
            }
            else if (mlType == MultilineType.NormalPreserveSpacing) // normal offset and preserve letter spacing
            {

            }

            else // parallel offset 
            {

                Vector3 lineOffset = new Vector3(0, VFBL.y - basePos.y, 0);
                basePos = (number == 0) ? basePos : basePos + lineOffset; // offset basepos 
                for (int i = 0; i < vChars.Count; i++)
                {
                    vChar = vChars[i];
                    oldPos = vChar.BL;
                    Vector2 charDistance = oldPos - basePos;
                    newPos = curve.GetPositionEL(charDistance.x, out rotDir) + new Vector3(-rotDir.y, rotDir.x, 0).normalized * charDistance.y + lineOffset; // position on source curve + ... + line offset

                    vChar.Move(newPos - oldPos);
                    if (rotate) vChar.Rotate(rotDir);
                }
            }
        }

        /// <summary>
        /// Align text lines along curve, with text alignment option
        /// </summary>
        /// <param name="curve"></param>
        /// <param name="text"></param>
        /// <param name="basePos"></param>
        /// <param name="rotate"></param>
        /// <param name="mlType"></param>
        /// <param name="number"></param>
        /// <returns></returns>
        public void ModifyAlongCurve(CatmullRommCurve curve, Text text, Vector3 basePos, float curveAnchor, bool rotate, int number)
        {
            VChar vChar;
            Vector3 newPos;
            Vector3 oldPos;
            Vector3 rotDir;
            TextAnchor textAnchor = text.alignment;
            RectTransform rT = text.GetComponent<RectTransform>();
            Vector3[] rTLocCorners = new Vector3[4];
            rT.GetLocalCorners(rTLocCorners);
           // Debug.Log("basepos: " + basePos + " ; localCorners : " + rTLocCorners[0] + " : " + rTLocCorners[1] + " : " + rTLocCorners[2] + " : " + rTLocCorners[3]);


            Vector3 lineOffset = new Vector3(0, VFBL.y - basePos.y, 0);
            basePos = (number == 0) ? basePos : basePos + lineOffset; // offset basepos 
            for (int i = 0; i < vChars.Count; i++)
            {
                vChar = vChars[i];
                oldPos = vChar.BL;
                Vector2 charDistance = oldPos - basePos;
                newPos = curve.GetPositionEL(charDistance.x + curve.LengthCPE * curveAnchor, out rotDir) + new Vector3(-rotDir.y, rotDir.x, 0).normalized * charDistance.y + lineOffset; // position on source curve + ... + line offset

                vChar.Move(newPos - oldPos);
                if (rotate) vChar.Rotate(rotDir);
            }
        }

        public void Add(VChar vChar)
        {
            vChars.Add(vChar);
        }

        public string CharsToString()
        {
            string res = "";

            if (vChars != null && vChars.Count > 0)
            {
                char[] ca = new char[vChars.Count];
                for (int i = 0; i < vChars.Count; i++)
                {
                    ca[i] = vChars[i].lChar;
                }
                res = new string(ca);
            }
            return res;
        }
    }
}




