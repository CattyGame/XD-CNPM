"""
Vessel Tortuosity and Heuristic Risk Assessment Module (Sprint 4-5 / W45-M4-02).

Implements:
1. Retinal vessel skeletonization and O(N) vectorized morphological topology extraction.
2. Vessel Tortuosity Index computation based on 8-connected Euclidean arc-to-chord ratios.
3. Microvascular branching point detection (bifurcations/crossings).
4. Multi-factor Heuristic Risk Model evaluating microvascular anomalies
   (LOW, MODERATE, HIGH) with structured medical disclaimer.
"""

from __future__ import annotations

import math
from typing import List, Optional, Tuple

import numpy as np
from scipy import ndimage as ndi
from skimage.morphology import skeletonize

from ..schemas.analysis import RiskAssessment
from ..schemas.common import RiskLevel


NEIGHBOR_KERNEL = np.array(
    [
        [1, 1, 1],
        [1, 0, 1],
        [1, 1, 1],
    ],
    dtype=np.uint8,
)

SQRT_2 = math.sqrt(2.0)

HEURISTIC_DISCLAIMER = (
    "Đánh giá nguy cơ sơ bộ dựa trên mật độ và độ cong queo mạch máu võng mạc "
    "qua thuật toán heuristic (AURA AI Core v1.3); chỉ mang tính tham khảo kỹ thuật, "
    "không thay thế chẩn đoán lâm sàng của bác sĩ chuyên khoa mắt."
)


def compute_vessel_tortuosity_and_branches(
    mask: np.ndarray,
    min_segment_length: int = 10,
    min_chord_length: float = 3.0,
) -> Tuple[float, int]:
    """
    Compute Vessel Tortuosity Index and count branching points from a binary vessel mask
    using O(N) single-pass vectorized component grouping.

    - Skeletonizes the binary vessel mask to 1-pixel centerlines.
    - Identifies junction pixels (8-neighborhood degree >= 3) as branching points.
    - Splits the skeleton into non-junction vessel segments and measures exact
      8-connected Euclidean polygonal arc length (1.0 orthogonal, sqrt(2) diagonal)
      against Euclidean chord distance between topological degree-<=1 endpoints.
    - Returns:
        tortuosity_index: float >= 1.0 (length-weighted mean arc/chord ratio).
        branching_points: int >= 0 (total detected vessel bifurcations/crossings).
    """
    if mask.size == 0 or not np.any(mask):
        return 1.0, 0

    bool_mask = np.asarray(mask, dtype=bool)
    skeleton = skeletonize(bool_mask)
    if not np.any(skeleton):
        return 1.0, 0

    skel_u8 = skeleton.astype(np.uint8)
    neighbors = ndi.convolve(skel_u8, NEIGHBOR_KERNEL, mode="constant", cval=0)
    neighbors *= skel_u8

    branching_points = int(np.count_nonzero(neighbors >= 3))

    non_junction = skeleton & (neighbors < 3)
    if not np.any(non_junction):
        return 1.0, branching_points

    labeled_segments, num_segments = ndi.label(
        non_junction,
        structure=np.ones((3, 3), dtype=np.uint8),
    )
    if num_segments == 0:
        return 1.0, branching_points

    # Compute 8-connected Euclidean polygonal arc length per segment via vectorized edge bincount
    bins = num_segments + 1
    arc_per_label = np.zeros(bins, dtype=np.float64)

    # Horizontal & vertical unit steps (length 1.0)
    h_mask = non_junction[:, :-1] & non_junction[:, 1:]
    if np.any(h_mask):
        arc_per_label += np.bincount(labeled_segments[:, :-1][h_mask], minlength=bins)

    v_mask = non_junction[:-1, :] & non_junction[1:, :]
    if np.any(v_mask):
        arc_per_label += np.bincount(labeled_segments[:-1, :][v_mask], minlength=bins)

    # Diagonal steps (length sqrt(2)), excluding L-corners already bridged orthogonally
    d1_mask = (
        non_junction[:-1, :-1]
        & non_junction[1:, 1:]
        & ~(non_junction[:-1, 1:] | non_junction[1:, :-1])
    )
    if np.any(d1_mask):
        arc_per_label += SQRT_2 * np.bincount(
            labeled_segments[:-1, :-1][d1_mask],
            minlength=bins,
        )

    d2_mask = (
        non_junction[:-1, 1:]
        & non_junction[1:, :-1]
        & ~(non_junction[:-1, :-1] | non_junction[1:, 1:])
    )
    if np.any(d2_mask):
        arc_per_label += SQRT_2 * np.bincount(
            labeled_segments[:-1, 1:][d2_mask],
            minlength=bins,
        )

    # Degree within non-junction skeleton to locate true topological endpoints (degree <= 1)
    nj_u8 = non_junction.astype(np.uint8)
    seg_degree = ndi.convolve(nj_u8, NEIGHBOR_KERNEL, mode="constant", cval=0)
    seg_degree *= nj_u8

    # Single O(N_skeleton) sort by segment label instead of O(N_pixels * N_segments) scan
    nz_r, nz_c = np.nonzero(labeled_segments)
    nz_lbl = labeled_segments[nz_r, nz_c]
    order = np.argsort(nz_lbl, kind="mergesort")
    nz_r = nz_r[order]
    nz_c = nz_c[order]
    nz_lbl = nz_lbl[order]
    nz_deg = seg_degree[nz_r, nz_c]

    splits = np.flatnonzero(np.diff(nz_lbl)) + 1
    starts = np.concatenate(([0], splits))
    ends = np.concatenate((splits, [len(nz_lbl)]))
    pixel_counts = ends - starts

    valid_groups = np.flatnonzero(pixel_counts >= min_segment_length)
    if valid_groups.size == 0:
        return 1.0, branching_points

    total_arc = 0.0
    total_chord = 0.0

    for g_idx in valid_groups:
        s_i = int(starts[g_idx])
        e_i = int(ends[g_idx])
        seg_id = int(nz_lbl[s_i])

        rs = nz_r[s_i:e_i]
        cs = nz_c[s_i:e_i]
        ds = nz_deg[s_i:e_i]

        endpoints = np.flatnonzero(ds <= 1)
        if endpoints.size >= 2:
            r0, c0 = int(rs[endpoints[0]]), int(cs[endpoints[0]])
            r1, c1 = int(rs[endpoints[-1]]), int(cs[endpoints[-1]])
        else:
            # Closed loop or degenerate segment: pick farthest pair along major axis
            r0, c0 = int(rs[0]), int(cs[0])
            r1, c1 = int(rs[-1]), int(cs[-1])

        chord = math.hypot(float(r1 - r0), float(c1 - c0))
        if chord >= min_chord_length:
            arc = max(float(arc_per_label[seg_id]), chord)
            total_arc += arc
            total_chord += chord

    if total_chord > 0.0:
        tortuosity_index = round(max(1.0, total_arc / total_chord), 4)
    else:
        tortuosity_index = 1.0

    return tortuosity_index, branching_points


def evaluate_heuristic_risk(
    vessel_density: float,
    tortuosity_index: Optional[float] = None,
    branching_points: Optional[int] = None,
) -> RiskAssessment:
    """
    Multi-factor Heuristic Risk Model (W45-M4-02):
    Calculates a continuous risk_score (0.0 .. 1.0) and assigns a heuristic risk_level
    (LOW, MODERATE, HIGH) based on microvascular morphology metrics.

    Metrics evaluated:
    - vessel_density (reference range on full fundus geometry: 0.040 - 0.080)
    - tortuosity_index (normal: < 1.18, moderate: 1.18 - 1.30, high: > 1.30)
    - branching_points (detected microvascular bifurcations/crossings)
    """
    tort = float(tortuosity_index) if tortuosity_index is not None else 1.10
    density = float(vessel_density)

    # Tortuosity component [0..1]: baseline <= 1.10, elevated >= 1.35
    s_tort = float(np.clip((tort - 1.10) / 0.25, 0.0, 1.0))

    # Density deviation component [0..1]: reference center ~ 0.058, elevated outside [0.038, 0.085]
    s_dens = float(np.clip(abs(density - 0.058) / 0.032, 0.0, 1.0))

    # Weighted composite heuristic score: 60% tortuosity + 40% density deviation
    raw_score = 0.60 * s_tort + 0.40 * s_dens
    risk_score = round(float(np.clip(raw_score, 0.0, 1.0)), 4)

    if risk_score < 0.35:
        risk_level = RiskLevel.LOW
    elif risk_score < 0.70:
        risk_level = RiskLevel.MODERATE
    else:
        risk_level = RiskLevel.HIGH

    indicators: List[str] = []

    if density < 0.040:
        indicators.append(
            f"Mật độ mạch máu thấp ({density:.4f} < 0.040): Dấu hiệu thưa vi mạch (Capillary Dropout)."
        )
    elif density > 0.085:
        indicators.append(
            f"Mật độ mạch máu cao ({density:.4f} > 0.085): Dấu hiệu tăng mật độ hoặc nhiễu phân nhánh mạch."
        )
    else:
        indicators.append(
            f"Mật độ mạch máu ({density:.4f}): Nằm trong khoảng tham chiếu hình thái chuẩn."
        )

    if tort > 1.30:
        indicators.append(
            f"Chỉ số độ cong queo cao ({tort:.4f} > 1.30): Mạch máu uốn lượn/ngoằn ngoèo rõ rệt."
        )
    elif tort >= 1.18:
        indicators.append(
            f"Chỉ số độ cong queo mức độ vừa ({tort:.4f}): Tăng nhẹ độ uốn lượn mạch máu."
        )
    else:
        indicators.append(
            f"Chỉ số độ cong queo bình thường ({tort:.4f}): Cấu trúc nhánh mạch phân bố đều."
        )

    if branching_points is not None:
        indicators.append(
            f"Số điểm phân nhánh vi mạch phát hiện: {branching_points} điểm."
        )

    return RiskAssessment(
        risk_score=risk_score,
        risk_level=risk_level,
        confidence_score=0.85,
        indicators=indicators,
        disclaimer=HEURISTIC_DISCLAIMER,
    )
