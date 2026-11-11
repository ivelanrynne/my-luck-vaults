using Godot;
using System;
using System.Collections.Generic;

namespace MyLuckVaults.Enemies.AI
{
    /// <summary>
    /// Loại ô địa hình trên lưới không gian 2D dùng cho AI tìm đường.
    /// </summary>
    public enum NavCellType : byte
    {
        Empty = 0,  // Không khí trống (có thể đi qua/bay qua tự do)
        Solid = 1,  // Tường, sàn đất, vật cản cứng (chặn hoàn toàn)
        OneWay = 2  // Sàn một chiều (nhảy lên xuyên qua được, nhưng đứng trên đỉnh hoặc rơi xuống sẽ bị chặn)
    }

    /// <summary>
    /// Kết quả tìm đường trả về cho từng con quái.
    /// </summary>
    public struct NavPathResult
    {
        public bool Success;
        public bool ReachedExactGoal;
        public List<Vector2> Waypoints;
    }

    /// <summary>
    /// Hệ thống quét bản đồ vật lý 2D động & Tìm đường thông minh A* (Shared Per-World Navigation Grid).
    /// 
    /// TÍNH NĂNG NỔI BẬT:
    /// 1. Tự động quét TileMap + StaticBody2D + Vật cản + Sàn một chiều theo thời gian thực qua PhysicsDirectSpaceState2D
    ///    mà KHÔNG cần vẽ NavigationRegion2D thủ công trong từng màn chơi.
    /// 2. Chia sẻ bộ nhớ đệm (Cache) theo từng World2D để hàng chục quái dùng chung dữ liệu bản đồ với chi phí CPU cực thấp.
    /// 3. Hỗ trợ 2 chế độ tìm đường độc lập:
    ///    - Quái Bay (Aerial A*): Bay 8 hướng, tránh cắt góc tường, tự giữ khoảng cách lề ở giữa hành lang/ống khói,
    ///      né sàn 1 chiều từ phía trên và làm mượt đường bay (String-Pulling Funnel Smoother).
    ///    - Quái Đi Bộ (Platformer A*): Tự nhận biết mặt sàn, nhảy qua vật cản/hố sâu, leo lên các bậc thềm cao,
    ///      và biết TÌM ĐƯỜNG VÒNG TỚI MIỆNG HỐ / ỐNG KHÓI ĐỂ RƠI XUỐNG HẦM săn đuổi người chơi!
    /// </summary>
    public static class EnemyNavigationGrid
    {
        public const float CellSize = 16.0f;
        private const float ProbeBoxSize = 14.2f; // Vừa khít lưới TileMap 16x16, tránh bắt nhầm ô lân cận

        // Cache địa hình theo từng World2D + CurrentScene Instance ID (tự động xóa khi đổi màn/portal)
        private static ulong _cachedWorldId = 0;
        private static ulong _cachedSceneId = 0;
        private static readonly Dictionary<Vector2I, NavCellType> _cellCache = new Dictionary<Vector2I, NavCellType>(4096);
        private static RectangleShape2D _sharedProbeShape;

        private static readonly Vector2I[] CardinalDirs =
        {
            new Vector2I(1, 0), new Vector2I(-1, 0),
            new Vector2I(0, 1), new Vector2I(0, -1)
        };

        private static readonly Vector2I[] All8Dirs =
        {
            new Vector2I(1, 0), new Vector2I(-1, 0), new Vector2I(0, 1), new Vector2I(0, -1),
            new Vector2I(1, 1), new Vector2I(-1, 1), new Vector2I(1, -1), new Vector2I(-1, -1)
        };

        public static Vector2I WorldToGrid(Vector2 worldPos)
        {
            return new Vector2I(
                Mathf.FloorToInt(worldPos.X / CellSize),
                Mathf.FloorToInt(worldPos.Y / CellSize)
            );
        }

        public static Vector2 GridToWorldCenter(Vector2I cell)
        {
            return new Vector2((cell.X + 0.5f) * CellSize, (cell.Y + 0.5f) * CellSize);
        }

        /// <summary>
        /// Làm mới bộ đệm lưới cục bộ quanh vị trí được chỉ định (hữu ích khi quái gặp kẹt bất thường).
        /// </summary>
        public static void InvalidateRegion(Vector2 worldPos, int radiusCells = 4)
        {
            Vector2I center = WorldToGrid(worldPos);
            for (int dx = -radiusCells; dx <= radiusCells; dx++)
            {
                for (int dy = -radiusCells; dy <= radiusCells; dy++)
                {
                    _cellCache.Remove(new Vector2I(center.X + dx, center.Y + dy));
                }
            }
        }

        /// <summary>
        /// Xóa toàn bộ cache (gọi khi đổi scene hoặc reset map).
        /// </summary>
        public static void ClearCache()
        {
            _cellCache.Clear();
            _cachedWorldId = 0;
            _cachedSceneId = 0;
        }

        private static void EnsureWorldCacheValid(World2D world)
        {
            if (world == null) return;
            ulong worldId = world.GetInstanceId();
            Node currentScene = (Engine.GetMainLoop() as SceneTree)?.CurrentScene;
            ulong sceneId = (currentScene != null && GodotObject.IsInstanceValid(currentScene))
                ? currentScene.GetInstanceId()
                : 0;

            bool sceneChanged = (sceneId != 0 && _cachedSceneId != sceneId);
            if (_cachedWorldId != worldId || sceneChanged || _cellCache.Count > 24000)
            {
                _cellCache.Clear();
                _cachedWorldId = worldId;
                if (sceneId != 0) _cachedSceneId = sceneId;
            }
            if (_sharedProbeShape == null)
            {
                _sharedProbeShape = new RectangleShape2D { Size = new Vector2(ProbeBoxSize, ProbeBoxSize) };
            }
        }

        /// <summary>
        /// Lấy loại ô tại tọa độ lưới (đọc từ Cache hoặc truy vấn vật lý Layer 1 nếu chưa có).
        /// </summary>
        public static NavCellType GetCell(PhysicsDirectSpaceState2D spaceState, World2D world, Vector2I cell)
        {
            EnsureWorldCacheValid(world);

            if (_cellCache.TryGetValue(cell, out NavCellType cached))
            {
                return cached;
            }

            if (spaceState == null) return NavCellType.Empty;

            var query = new PhysicsShapeQueryParameters2D
            {
                Shape = _sharedProbeShape,
                Transform = new Transform2D(0f, GridToWorldCenter(cell)),
                CollisionMask = 1, // Layer 1: Địa hình, Tường, Sàn, Vật cản
                CollideWithBodies = true,
                CollideWithAreas = false
            };

            var hits = spaceState.IntersectShape(query, 8);
            NavCellType result = NavCellType.Empty;

            if (hits != null && hits.Count > 0)
            {
                bool hasSolid = false;
                bool hasOneWay = false;

                for (int i = 0; i < hits.Count; i++)
                {
                    var hit = hits[i];
                    if (!hit.ContainsKey("collider")) continue;

                    GodotObject colliderObj = hit["collider"].AsGodotObject();
                    int shapeIdx = hit.ContainsKey("shape") ? hit["shape"].AsInt32() : 0;

                    if (colliderObj is CollisionObject2D colObj)
                    {
                        uint ownerId = colObj.ShapeFindOwner(shapeIdx);
                        if (colObj.IsShapeOwnerOneWayCollisionEnabled(ownerId))
                        {
                            hasOneWay = true;
                        }
                        else
                        {
                            hasSolid = true;
                            break;
                        }
                    }
                    else
                    {
                        // TileMap hoặc vật thể tĩnh mặc định là Solid
                        hasSolid = true;
                        break;
                    }
                }

                if (hasSolid) result = NavCellType.Solid;
                else if (hasOneWay) result = NavCellType.OneWay;
            }

            _cellCache[cell] = result;
            return result;
        }

        /// <summary>
        /// Kiểm tra một ô có đủ khoảng trống cho kích thước cơ thể quái đi qua không.
        /// Tránh việc quái to cố chui qua khe hẹp 1 ô (32px).
        /// </summary>
        public static bool IsCellPassableForBody(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2I cell,
            float bodyHalfWidth,
            float bodyHalfHeight,
            bool allowOneWayCell)
        {
            NavCellType centerType = GetCell(spaceState, world, cell);
            if (centerType == NavCellType.Solid) return false;
            if (!allowOneWayCell && centerType == NavCellType.OneWay) return false;

            // Tính số ô 16px ngang/dọc tối thiểu theo kích thước hitbox thực tế của quái
            int reqHorizCells = Mathf.Max(1, Mathf.CeilToInt((bodyHalfWidth * 2f - 1f) / CellSize));
            int reqVertCells = Mathf.Max(1, Mathf.CeilToInt((bodyHalfHeight * 2f - 1f) / CellSize));

            if (reqHorizCells > 1)
            {
                int maxReach = reqHorizCells - 1;
                int openH = 1;
                for (int l = 1; l <= maxReach; l++)
                {
                    if (GetCell(spaceState, world, new Vector2I(cell.X - l, cell.Y)) != NavCellType.Solid) openH++;
                    else break;
                }
                for (int r = 1; r <= maxReach && openH < reqHorizCells; r++)
                {
                    if (GetCell(spaceState, world, new Vector2I(cell.X + r, cell.Y)) != NavCellType.Solid) openH++;
                    else break;
                }
                if (openH < reqHorizCells) return false;
            }

            if (reqVertCells > 1)
            {
                int maxReach = reqVertCells - 1;
                int openV = 1;
                for (int u = 1; u <= maxReach; u++)
                {
                    if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y - u)) != NavCellType.Solid) openV++;
                    else break;
                }
                for (int d = 1; d <= maxReach && openV < reqVertCells; d++)
                {
                    if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y + d)) != NavCellType.Solid) openV++;
                    else break;
                }
                if (openV < reqVertCells) return false;
            }

            return true;
        }

        /// <summary>
        /// Tìm ô gần nhất có thể đứng/bay được (dùng khi Start hoặc Goal bị kẹt sát tường/đất).
        /// </summary>
        private static Vector2I FindNearestPassableCell(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2I origin,
            float halfW,
            float halfH,
            bool allowOneWay)
        {
            if (IsCellPassableForBody(spaceState, world, origin, halfW, halfH, allowOneWay))
            {
                return origin;
            }

            // Quét xoắn ốc theo từng vòng bán kính tối đa 4 ô (64px), ưu tiên hướng lên trên trước
            for (int radius = 1; radius <= 4; radius++)
            {
                // Ưu tiên phía trên trước (trường hợp đứng sát mặt đất)
                Vector2I up = new Vector2I(origin.X, origin.Y - radius);
                if (IsCellPassableForBody(spaceState, world, up, halfW, halfH, allowOneWay)) return up;

                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                        Vector2I candidate = new Vector2I(origin.X + dx, origin.Y + dy);
                        if (IsCellPassableForBody(spaceState, world, candidate, halfW, halfH, allowOneWay))
                        {
                            return candidate;
                        }
                    }
                }
            }

            return origin;
        }

        /// <summary>
        /// Tính toán tọa độ thế giới tối ưu cho một ô trên đường đi, tự động đẩy tâm điểm ra xa tường
        /// để quái đi lọt qua các ống khói hay hành lang hẹp mà không bị cạ mép.
        /// </summary>
        private static Vector2 GetWallClearedWorldPos(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2I cell,
            float halfW,
            float halfH)
        {
            Vector2 center = GridToWorldCenter(cell);

            int leftWallDist = 0;
            if (GetCell(spaceState, world, new Vector2I(cell.X - 1, cell.Y)) == NavCellType.Solid) leftWallDist = 1;
            else if (GetCell(spaceState, world, new Vector2I(cell.X - 2, cell.Y)) == NavCellType.Solid) leftWallDist = 2;

            int rightWallDist = 0;
            if (GetCell(spaceState, world, new Vector2I(cell.X + 1, cell.Y)) == NavCellType.Solid) rightWallDist = 1;
            else if (GetCell(spaceState, world, new Vector2I(cell.X + 2, cell.Y)) == NavCellType.Solid) rightWallDist = 2;

            float reqMarginX = Mathf.Max(0f, halfW - CellSize * 0.5f);
            if (leftWallDist > 0 && rightWallDist == 0)
            {
                float curGap = (leftWallDist - 0.5f) * CellSize;
                center.X += Mathf.Clamp(reqMarginX - curGap + 2f, 2f, CellSize * 1.25f);
            }
            else if (rightWallDist > 0 && leftWallDist == 0)
            {
                float curGap = (rightWallDist - 0.5f) * CellSize;
                center.X -= Mathf.Clamp(reqMarginX - curGap + 2f, 2f, CellSize * 1.25f);
            }

            int upWallDist = 0;
            if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y - 1)) == NavCellType.Solid) upWallDist = 1;
            else if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y - 2)) == NavCellType.Solid) upWallDist = 2;

            int downWallDist = 0;
            if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y + 1)) == NavCellType.Solid) downWallDist = 1;
            else if (GetCell(spaceState, world, new Vector2I(cell.X, cell.Y + 2)) == NavCellType.Solid) downWallDist = 2;

            float reqMarginY = Mathf.Max(0f, halfH - CellSize * 0.5f);
            if (upWallDist > 0 && downWallDist == 0)
            {
                float curGap = (upWallDist - 0.5f) * CellSize;
                center.Y += Mathf.Clamp(reqMarginY - curGap + 2f, 2f, CellSize * 1.25f);
            }
            else if (downWallDist > 0 && upWallDist == 0)
            {
                float curGap = (downWallDist - 0.5f) * CellSize;
                center.Y -= Mathf.Clamp(reqMarginY - curGap + 2f, 2f, CellSize * 1.25f);
            }

            return center;
        }

        /// <summary>
        /// Kiểm tra đường bay thẳng giữa 2 điểm trên lưới có hoàn toàn thông thoáng không (dùng để làm mượt đường bay).
        /// Tính theo đúng hình chiếu kích thước thân quái theo phương vuông góc đường bay.
        /// </summary>
        public static bool HasClearFlightLine(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2 fromWorld,
            Vector2 toWorld,
            float halfW,
            float halfH)
        {
            if (spaceState == null) return true;

            float dist = fromWorld.DistanceTo(toWorld);
            if (dist < 10f) return true;

            int steps = Mathf.CeilToInt(dist / 10.0f);
            Vector2 delta = (toWorld - fromWorld) / steps;
            Vector2 dir = (toWorld - fromWorld) / dist;
            bool movingDown = dir.Y > 0.05f;

            // Tính nửa bề rộng hiệu dụng của quái vuông góc với vector hướng bay (hỗ trợ cả hitbox xoay như Harpy)
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float perpHalf = Mathf.Abs(perp.X) * Mathf.Max(2f, halfW - 3f)
                           + Mathf.Abs(perp.Y) * Mathf.Max(2f, halfH - 3f);
            bool checkMidLines = perpHalf > 14f;

            for (int s = 1; s <= steps; s++)
            {
                Vector2 pCenter = fromWorld + delta * s;
                if (!IsPointClearForFlight(spaceState, world, pCenter, movingDown)) return false;
                if (!IsPointClearForFlight(spaceState, world, pCenter + perp * perpHalf, movingDown)) return false;
                if (!IsPointClearForFlight(spaceState, world, pCenter - perp * perpHalf, movingDown)) return false;

                if (checkMidLines)
                {
                    if (!IsPointClearForFlight(spaceState, world, pCenter + perp * (perpHalf * 0.5f), movingDown)) return false;
                    if (!IsPointClearForFlight(spaceState, world, pCenter - perp * (perpHalf * 0.5f), movingDown)) return false;
                }
            }

            return true;
        }

        private static bool IsPointClearForFlight(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2 pos,
            bool movingDown)
        {
            Vector2I cell = WorldToGrid(pos);
            NavCellType type = GetCell(spaceState, world, cell);
            if (type == NavCellType.Solid) return false;
            if (type == NavCellType.OneWay && movingDown) return false;
            return true;
        }

        // =========================================================================
        // 1. THUẬT TOÁN TÌM ĐƯỜNG CHO QUÁI BAY (AERIAL A* + FUNNEL SMOOTHER)
        // =========================================================================
        public static NavPathResult FindFlyingPath(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2 startWorld,
            Vector2 goalWorld,
            float bodyHalfWidth,
            float bodyHalfHeight,
            int maxIterations = 4500)
        {
            EnsureWorldCacheValid(world);

            Vector2I rawStart = WorldToGrid(startWorld);
            Vector2I rawGoal = WorldToGrid(goalWorld);

            Vector2I start = FindNearestPassableCell(spaceState, world, rawStart, bodyHalfWidth, bodyHalfHeight, true);
            Vector2I goal = FindNearestPassableCell(spaceState, world, rawGoal, bodyHalfWidth, bodyHalfHeight, true);

            if (start == goal)
            {
                return new NavPathResult
                {
                    Success = true,
                    ReachedExactGoal = true,
                    Waypoints = new List<Vector2> { goalWorld }
                };
            }

            // Vùng giới hạn tìm kiếm động bao quanh quái và người chơi (lưới 16px)
            int minX = Mathf.Min(start.X, goal.X) - 70;
            int maxX = Mathf.Max(start.X, goal.X) + 70;
            int minY = Mathf.Min(start.Y, goal.Y) - 55;
            int maxY = Mathf.Max(start.Y, goal.Y) + 55;

            var openSet = new PriorityQueue<(Vector2I Cell, float G), float>();
            var cameFrom = new Dictionary<Vector2I, Vector2I>(512);
            var gScore = new Dictionary<Vector2I, float>(512);

            gScore[start] = 0f;
            float startH = OctileHeuristic(start, goal);
            openSet.Enqueue((start, 0f), startH);

            Vector2I bestFallback = start;
            float bestFallbackH = startH;
            bool foundGoal = false;
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                var popped = openSet.Dequeue();
                Vector2I current = popped.Cell;

                if (popped.G > gScore[current] + 1e-4f) continue;

                if (current == goal)
                {
                    foundGoal = true;
                    break;
                }

                float currentG = popped.G;

                for (int i = 0; i < All8Dirs.Length; i++)
                {
                    Vector2I d = All8Dirs[i];
                    Vector2I next = current + d;

                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY) continue;

                    // Kiểm tra thân quái có lọt qua ô tiếp theo không (trừ ô goal/start đã snap)
                    if (next != goal && !IsCellPassableForBody(spaceState, world, next, bodyHalfWidth, bodyHalfHeight, true))
                    {
                        continue;
                    }

                    NavCellType nextType = GetCell(spaceState, world, next);
                    if (nextType == NavCellType.Solid) continue;

                    // Sàn một chiều: cho phép bay ngang hoặc bay lên xuyên qua, nhưng chặn hướng lao xuống từ trên
                    if (nextType == NavCellType.OneWay && d.Y > 0) continue;

                    // Chống cắt góc chéo (Diagonal Corner-Cutting Prevention)
                    bool isDiag = (d.X != 0 && d.Y != 0);
                    if (isDiag)
                    {
                        NavCellType sideX = GetCell(spaceState, world, new Vector2I(current.X + d.X, current.Y));
                        NavCellType sideY = GetCell(spaceState, world, new Vector2I(current.X, current.Y + d.Y));
                        if (sideX == NavCellType.Solid || sideY == NavCellType.Solid) continue;
                        if (d.Y > 0 && (sideX == NavCellType.OneWay || sideY == NavCellType.OneWay)) continue;
                    }

                    // Phạt nhẹ khi bay sát tường (trong phạm vi 2 ô) để quái bay ở chính giữa ống khói / hành lang
                    float wallPenalty = 0f;
                    if (next != goal)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            if (GetCell(spaceState, world, next + CardinalDirs[c]) == NavCellType.Solid)
                            {
                                wallPenalty += 0.6f;
                            }
                            else if (GetCell(spaceState, world, next + CardinalDirs[c] * 2) == NavCellType.Solid)
                            {
                                wallPenalty += 0.2f;
                            }
                        }
                    }

                    float moveCost = isDiag ? 1.4142f : 1.0f;
                    float tentativeG = currentG + moveCost + wallPenalty;

                    if (!gScore.TryGetValue(next, out float existingG) || tentativeG < existingG)
                    {
                        cameFrom[next] = current;
                        gScore[next] = tentativeG;

                        float h = OctileHeuristic(next, goal);
                        openSet.Enqueue((next, tentativeG), tentativeG + h * 1.18f);

                        if (h < bestFallbackH)
                        {
                            bestFallbackH = h;
                            bestFallback = next;
                        }
                    }
                }
            }

            Vector2I endCell = foundGoal ? goal : bestFallback;
            if (endCell == start)
            {
                return new NavPathResult
                {
                    Success = false,
                    ReachedExactGoal = false,
                    Waypoints = new List<Vector2>()
                };
            }

            // Truy vết ngược lại đường đi trên lưới
            var gridPath = new List<Vector2I>(64);
            Vector2I trace = endCell;
            int safety = 0;
            while (trace != start && safety < 4000)
            {
                gridPath.Add(trace);
                if (!cameFrom.TryGetValue(trace, out trace)) break;
                safety++;
            }
            gridPath.Reverse();

            // Chuyển sang tọa độ thế giới có tự động đẩy xa mép tường
            var rawPoints = new List<Vector2>(gridPath.Count + 1);
            for (int i = 0; i < gridPath.Count; i++)
            {
                Vector2 wp = GetWallClearedWorldPos(spaceState, world, gridPath[i], bodyHalfWidth, bodyHalfHeight);
                rawPoints.Add(wp);
            }
            if (foundGoal)
            {
                rawPoints[rawPoints.Count - 1] = goalWorld;
            }

            // Làm mượt đường bay (String-Pulling): Bỏ qua các điểm trung gian nếu bay thẳng được
            var smoothed = SmoothFlightPath(spaceState, world, startWorld, rawPoints, bodyHalfWidth, bodyHalfHeight);

            return new NavPathResult
            {
                Success = smoothed.Count > 0,
                ReachedExactGoal = foundGoal,
                Waypoints = smoothed
            };
        }

        private static List<Vector2> SmoothFlightPath(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2 startWorld,
            List<Vector2> points,
            float halfW,
            float halfH)
        {
            if (points.Count <= 1) return points;

            var result = new List<Vector2>(points.Count);
            Vector2 anchor = startWorld;
            int idx = 0;

            while (idx < points.Count)
            {
                int furthest = idx;
                // Thử nhìn xa tối đa 16 bước (256px) để cắt gọn đường bay ziczac
                int maxLook = Mathf.Min(points.Count - 1, idx + 16);
                for (int probe = maxLook; probe > idx; probe--)
                {
                    if (HasClearFlightLine(spaceState, world, anchor, points[probe], halfW, halfH))
                    {
                        furthest = probe;
                        break;
                    }
                }

                result.Add(points[furthest]);
                anchor = points[furthest];
                idx = furthest + 1;
            }

            return result;
        }

        // =========================================================================
        // 2. THUẬT TOÁN TÌM ĐƯỜNG CHO QUÁI ĐI BỘ (PLATFORMER A*)
        //    Hỗ trợ: Leo thềm cao, nhảy hố/vật cản, tìm đường đi vòng xuống ống khói/hầm!
        // =========================================================================
        private readonly struct PlatformerNodeKey : IEquatable<PlatformerNodeKey>
        {
            public readonly Vector2I Cell;
            public readonly int AirBudget;

            public PlatformerNodeKey(Vector2I cell, int airBudget)
            {
                Cell = cell;
                AirBudget = airBudget;
            }

            public bool Equals(PlatformerNodeKey other)
                => Cell.Equals(other.Cell) && AirBudget == other.AirBudget;

            public override bool Equals(object obj)
                => obj is PlatformerNodeKey other && Equals(other);

            public override int GetHashCode()
                => HashCode.Combine(Cell.X, Cell.Y, AirBudget);
        }

        public static NavPathResult FindGroundPath(
            PhysicsDirectSpaceState2D spaceState,
            World2D world,
            Vector2 startWorld,
            Vector2 goalWorld,
            float bodyHalfWidth,
            float bodyHalfHeight,
            int maxJumpSteps = 16,
            int maxIterations = 5000)
        {
            EnsureWorldCacheValid(world);

            Vector2I rawStart = WorldToGrid(startWorld);
            Vector2I rawGoal = WorldToGrid(goalWorld);

            Vector2I start = FindNearestPassableCell(spaceState, world, rawStart, bodyHalfWidth, bodyHalfHeight, true);
            Vector2I goal = FindNearestPassableCell(spaceState, world, rawGoal, bodyHalfWidth, bodyHalfHeight, true);

            if (start == goal)
            {
                return new NavPathResult
                {
                    Success = true,
                    ReachedExactGoal = true,
                    Waypoints = new List<Vector2> { goalWorld }
                };
            }

            int minX = Mathf.Min(start.X, goal.X) - 72;
            int maxX = Mathf.Max(start.X, goal.X) + 72;
            int minY = Mathf.Min(start.Y, goal.Y) - 48;
            int maxY = Mathf.Max(start.Y, goal.Y) + 55;

            bool IsGroundedCell(Vector2I c)
            {
                NavCellType below = GetCell(spaceState, world, new Vector2I(c.X, c.Y + 1));
                return below == NavCellType.Solid || below == NavCellType.OneWay;
            }

            int startBudget = IsGroundedCell(start) ? maxJumpSteps : Mathf.Max(4, maxJumpSteps / 2);
            var startKey = new PlatformerNodeKey(start, startBudget);

            var openSet = new PriorityQueue<(PlatformerNodeKey State, float G), float>();
            var cameFrom = new Dictionary<PlatformerNodeKey, PlatformerNodeKey>(512);
            var bestStateG = new Dictionary<PlatformerNodeKey, float>(512);
            var cellBestInfo = new Dictionary<Vector2I, (float BestG, int BudgetAtBestG)>(512);

            bestStateG[startKey] = 0f;
            cellBestInfo[start] = (0f, startBudget);
            float startH = ManhattanPlatformerHeuristic(start, goal);
            openSet.Enqueue((startKey, 0f), startH);

            PlatformerNodeKey bestFallbackState = startKey;
            float bestFallbackH = startH;
            PlatformerNodeKey goalReachedState = startKey;
            bool foundGoal = false;
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;
                var popped = openSet.Dequeue();
                PlatformerNodeKey curKey = popped.State;
                float curG = popped.G;

                if (curG > bestStateG[curKey] + 1e-4f) continue;

                Vector2I c = curKey.Cell;
                if (c == goal)
                {
                    foundGoal = true;
                    goalReachedState = curKey;
                    break;
                }

                bool onFloor = IsGroundedCell(c);
                int budget = onFloor ? maxJumpSteps : curKey.AirBudget;

                // Các nước đi hợp lệ của nhân vật đi bộ:
                // 1. Đi ngang trái/phải
                // 2. Nhảy thẳng lên / nhảy chéo lên (tốn 2 budget)
                // 3. Rơi thẳng xuống / rơi chéo xuống (dùng để xuống ống khói/hầm!)
                for (int i = 0; i < All8Dirs.Length; i++)
                {
                    Vector2I d = All8Dirs[i];
                    Vector2I next = c + d;

                    if (next.X < minX || next.X > maxX || next.Y < minY || next.Y > maxY) continue;

                    NavCellType nextType = GetCell(spaceState, world, next);
                    if (nextType == NavCellType.Solid) continue;

                    // Không cho phép rơi xuyên xuống qua sàn OneWay
                    if (d.Y > 0 && nextType == NavCellType.OneWay) continue;

                    if (next != goal && !IsCellPassableForBody(spaceState, world, next, bodyHalfWidth, bodyHalfHeight, true))
                    {
                        continue;
                    }

                    // Kiểm tra va chạm góc khi nhảy chéo hoặc rơi chéo
                    if (d.X != 0 && d.Y != 0)
                    {
                        NavCellType sideX = GetCell(spaceState, world, new Vector2I(c.X + d.X, c.Y));
                        NavCellType sideY = GetCell(spaceState, world, new Vector2I(c.X, c.Y + d.Y));
                        if (sideX == NavCellType.Solid || sideY == NavCellType.Solid) continue;
                        if (d.Y > 0 && sideY == NavCellType.OneWay) continue;
                    }

                    bool nextOnFloor = IsGroundedCell(next);
                    int nextBudget;
                    float stepCost;

                    if (d.Y < 0)
                    {
                        if (budget < 2) continue;
                        nextBudget = nextOnFloor ? maxJumpSteps : (budget - 2);
                        stepCost = (d.X == 0) ? 2.1f : 2.4f;
                    }
                    else if (d.Y > 0)
                    {
                        nextBudget = nextOnFloor ? maxJumpSteps : 0;
                        stepCost = (d.X == 0) ? 1.15f : 1.35f;
                    }
                    else
                    {
                        if (onFloor && nextOnFloor)
                        {
                            nextBudget = maxJumpSteps;
                            stepCost = 1.0f; // Đi bộ trên sàn luôn được ưu tiên nhất
                        }
                        else if (onFloor && !nextOnFloor)
                        {
                            nextBudget = maxJumpSteps - 1;
                            stepCost = 1.25f;
                        }
                        else
                        {
                            if (budget < 1) continue;
                            nextBudget = nextOnFloor ? maxJumpSteps : (budget - 1);
                            stepCost = 1.5f;
                        }
                    }

                    float tentativeG = curG + stepCost;

                    // Cắt tỉa trạng thái bị thống trị hoàn toàn (g tệ hơn/bằng VÀ budget ít hơn/bằng)
                    if (cellBestInfo.TryGetValue(next, out var cellInfo))
                    {
                        if (tentativeG >= cellInfo.BestG && nextBudget <= cellInfo.BudgetAtBestG)
                        {
                            continue;
                        }
                        if (tentativeG < cellInfo.BestG)
                        {
                            cellBestInfo[next] = (tentativeG, nextBudget);
                        }
                    }
                    else
                    {
                        cellBestInfo[next] = (tentativeG, nextBudget);
                    }

                    var nextKey = new PlatformerNodeKey(next, nextBudget);
                    if (!bestStateG.TryGetValue(nextKey, out float oldStateG) || tentativeG < oldStateG)
                    {
                        // Do tentativeG chỉ cập nhật theo hướng giảm nghiêm ngặt và stepCost >= 1.0, cameFrom hoàn toàn không bao giờ tạo vòng lặp!
                        cameFrom[nextKey] = curKey;
                        bestStateG[nextKey] = tentativeG;

                        float h = ManhattanPlatformerHeuristic(next, goal);
                        openSet.Enqueue((nextKey, tentativeG), tentativeG + h * 1.15f);

                        if (h < bestFallbackH)
                        {
                            bestFallbackH = h;
                            bestFallbackState = nextKey;
                        }
                    }
                }
            }

            PlatformerNodeKey endState = foundGoal ? goalReachedState : bestFallbackState;
            if (endState.Cell == start)
            {
                return new NavPathResult
                {
                    Success = false,
                    ReachedExactGoal = false,
                    Waypoints = new List<Vector2>()
                };
            }

            var gridPath = new List<Vector2I>(64);
            PlatformerNodeKey trace = endState;
            int safety = 0;
            while (!trace.Equals(startKey) && safety < 4000)
            {
                gridPath.Add(trace.Cell);
                if (!cameFrom.TryGetValue(trace, out trace)) break;
                safety++;
            }
            gridPath.Reverse();

            // Chuyển thành tọa độ thế giới (nắn X ra khỏi mép tường để rơi thẳng lọt ống khói 64px, giữ nguyên Y của sàn)
            var waypoints = new List<Vector2>(gridPath.Count);
            for (int i = 0; i < gridPath.Count; i++)
            {
                Vector2 cleared = GetWallClearedWorldPos(spaceState, world, gridPath[i], bodyHalfWidth, bodyHalfHeight);
                Vector2 center = GridToWorldCenter(gridPath[i]);
                waypoints.Add(new Vector2(cleared.X, center.Y));
            }
            if (foundGoal && waypoints.Count > 0)
            {
                waypoints[waypoints.Count - 1] = goalWorld;
            }

            return new NavPathResult
            {
                Success = waypoints.Count > 0,
                ReachedExactGoal = foundGoal,
                Waypoints = waypoints
            };
        }

        private static float OctileHeuristic(Vector2I a, Vector2I b)
        {
            float dx = Mathf.Abs(a.X - b.X);
            float dy = Mathf.Abs(a.Y - b.Y);
            float min = Mathf.Min(dx, dy);
            float max = Mathf.Max(dx, dy);
            return 1.4142f * min + (max - min);
        }

        private static float ManhattanPlatformerHeuristic(Vector2I a, Vector2I b)
        {
            float dx = Mathf.Abs(a.X - b.X);
            float dy = Mathf.Abs(a.Y - b.Y);
            // Đi lên tốn nhiều sức hơn đi xuống
            float dyWeighted = (b.Y < a.Y) ? dy * 1.5f : dy * 1.05f;
            return dx + dyWeighted;
        }
    }
}
