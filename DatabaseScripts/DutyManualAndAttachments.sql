-- Duty manual tables (MySQL 8+).
-- A category is an independent row.  PARENT_CATEGORY_ID forms the tree and
-- SORT_ORD controls the order among siblings; names are never used as keys.

CREATE TABLE IF NOT EXISTS MODUWMNL_CATEGORY (
    CATEGORY_ID        BIGINT NOT NULL AUTO_INCREMENT,
    PARENT_CATEGORY_ID BIGINT NULL,
    CATEGORY_NM        VARCHAR(100) NOT NULL,
    SORT_ORD           INT NOT NULL DEFAULT 0,
    USE_YN             CHAR(1) NOT NULL DEFAULT 'Y',
    FSR_DTM            DATETIME NOT NULL,
    FSR_STF_NO         VARCHAR(30) NULL,
    LSH_DTM            DATETIME NOT NULL,
    LSH_STF_NO         VARCHAR(30) NULL,
    PRIMARY KEY (CATEGORY_ID),
    CONSTRAINT FK_EZHOWTOUSE_MWC_PARENT_V1
        FOREIGN KEY (PARENT_CATEGORY_ID) REFERENCES MODUWMNL_CATEGORY (CATEGORY_ID),
    KEY IX_MODUWMNL_CATEGORY_TREE (PARENT_CATEGORY_ID, USE_YN, SORT_ORD)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS MODUWMNL (
    MANUAL_ID       BIGINT NOT NULL AUTO_INCREMENT,
    CATEGORY_ID     BIGINT NOT NULL,
    MANUAL_TITLE    VARCHAR(200) NOT NULL,
    MANUAL_CONTENT  TEXT NOT NULL,
    CHECK_QUERY     TEXT NULL,
    USE_YN          CHAR(1) NOT NULL DEFAULT 'Y',
    LST_YN          CHAR(1) NOT NULL DEFAULT 'Y',
    FSR_DTM         DATETIME NOT NULL,
    FSR_STF_NO      VARCHAR(30) NULL,
    FSR_PRGM_NM     VARCHAR(100) NULL,
    FSR_IP_ADDR     VARCHAR(50) NULL,
    LSH_DTM         DATETIME NOT NULL,
    LSH_STF_NO      VARCHAR(30) NULL,
    LSH_PRGM_NM     VARCHAR(100) NULL,
    LSH_IP_ADDR     VARCHAR(50) NULL,
    PRIMARY KEY (MANUAL_ID),
    CONSTRAINT FK_EZHOWTOUSE_MNL_CATEGORY_V1
        FOREIGN KEY (CATEGORY_ID) REFERENCES MODUWMNL_CATEGORY (CATEGORY_ID),
    KEY IX_MODUWMNL_01 (USE_YN, LST_YN, CATEGORY_ID)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO MODUWMNL_CATEGORY
    (PARENT_CATEGORY_ID, CATEGORY_NM, SORT_ORD, USE_YN, FSR_DTM, FSR_STF_NO, LSH_DTM, LSH_STF_NO)
SELECT NULL, '기본 업무', 1, 'Y', NOW(), 'system', NOW(), 'system'
WHERE NOT EXISTS (
    SELECT 1 FROM MODUWMNL_CATEGORY
    WHERE PARENT_CATEGORY_ID IS NULL AND CATEGORY_NM = '기본 업무' AND USE_YN = 'Y'
);

-- Example root children. These inserts are idempotent, so the script can be run again safely.
INSERT INTO MODUWMNL_CATEGORY
    (PARENT_CATEGORY_ID, CATEGORY_NM, SORT_ORD, USE_YN, FSR_DTM, FSR_STF_NO, LSH_DTM, LSH_STF_NO)
SELECT root.CATEGORY_ID, seed.CATEGORY_NM, seed.SORT_ORD, 'Y', NOW(), 'system', NOW(), 'system'
FROM (
    SELECT '진료간호' AS CATEGORY_NM, 1 AS SORT_ORD
    UNION ALL SELECT '원무', 2
    UNION ALL SELECT '보험', 3
    UNION ALL SELECT '진료지원', 4
) seed
JOIN MODUWMNL_CATEGORY root
  ON root.PARENT_CATEGORY_ID IS NULL AND root.CATEGORY_NM = '기본 업무' AND root.USE_YN = 'Y'
WHERE NOT EXISTS (
    SELECT 1
    FROM MODUWMNL_CATEGORY child
    WHERE child.PARENT_CATEGORY_ID = root.CATEGORY_ID
      AND child.CATEGORY_NM = seed.CATEGORY_NM
      AND child.USE_YN = 'Y'
);

-- Example manuals. Replace these with your hospital's real workflow after reviewing them.
INSERT INTO MODUWMNL
    (CATEGORY_ID, MANUAL_TITLE, MANUAL_CONTENT, CHECK_QUERY, USE_YN, LST_YN,
     FSR_DTM, FSR_STF_NO, FSR_PRGM_NM, FSR_IP_ADDR,
     LSH_DTM, LSH_STF_NO, LSH_PRGM_NM, LSH_IP_ADDR)
SELECT category.CATEGORY_ID,
       seed.MANUAL_TITLE,
       seed.MANUAL_CONTENT,
       seed.CHECK_QUERY,
       'Y', 'Y', NOW(), 'system', 'seed', '', NOW(), 'system', 'seed', ''
FROM (
    SELECT '진료간호' AS CATEGORY_NM,
           '외래 진료 시작 전 점검' AS MANUAL_TITLE,
           '1. 당일 예약·접수 현황을 확인합니다.\n2. 진료실 비품과 장비의 이상 유무를 확인합니다.\n3. 응급 연락망과 대체 인력을 확인합니다.\n4. 특이사항은 인수인계 노트에 남깁니다.' AS MANUAL_CONTENT,
           '/* 당일 예약 현황은 병원 운영 시스템에서 확인하세요. */' AS CHECK_QUERY
    UNION ALL
    SELECT '진료간호',
           '검체 채취 및 이송 확인',
           '1. 환자 정보와 처방을 두 가지 이상 식별자로 대조합니다.\n2. 검체 용기·라벨·채취 시간을 확인합니다.\n3. 이송 기준 시간 내 전달 여부를 확인합니다.\n4. 오류 또는 지연은 즉시 담당자에게 공유합니다.',
           '/* 검체 이송 지연 건을 업무 시스템에서 확인하세요. */'
    UNION ALL
    SELECT '원무',
           '외래 접수 마감 절차',
           '1. 미수·보증금·서류 보완 대상자를 확인합니다.\n2. 당일 접수 취소 및 변경 내역을 점검합니다.\n3. 마감 전표와 수납 금액을 대조합니다.\n4. 차이가 있으면 사유와 조치 내용을 기록합니다.',
           '/* 수납 및 미수금은 권한이 있는 시스템 화면에서 확인하세요. */'
    UNION ALL
    SELECT '원무',
           '진단서·증명서 발급 안내',
           '1. 본인 또는 적법한 대리인 여부를 확인합니다.\n2. 신청 목적과 필요한 서류를 안내합니다.\n3. 의료진 확인이 필요한 문서는 발급 요청으로 전달합니다.\n4. 발급 이력과 수령 확인을 남깁니다.',
           '/* 개인정보가 포함된 발급 이력은 승인된 시스템에서만 조회하세요. */'
    UNION ALL
    SELECT '보험',
           '보험 청구 전 사전 점검',
           '1. 청구 대상 기간과 진료 내역을 확인합니다.\n2. 누락 처방·수가·서식을 점검합니다.\n3. 삭감 이력 또는 보완 요청 여부를 확인합니다.\n4. 제출 전 담당자 2차 검토를 진행합니다.',
           '/* 청구 오류 현황은 보험 청구 시스템의 검증 결과를 사용하세요. */'
    UNION ALL
    SELECT '보험',
           '삭감 통보 접수 및 대응',
           '1. 통보 일자와 대상 건을 기록합니다.\n2. 삭감 사유와 근거 자료를 확인합니다.\n3. 이의신청 또는 보완 청구 일정을 정합니다.\n4. 처리 결과와 재발 방지 사항을 공유합니다.',
           '/* 삭감 건은 민감정보를 제외한 통계 중심으로 관리하세요. */'
    UNION ALL
    SELECT '진료지원',
           '검사 장비 일일 점검',
           '1. 전원·소모품·오류 메시지를 확인합니다.\n2. 품질관리 결과를 기준값과 비교합니다.\n3. 이상 시 장비 사용을 중지하고 담당자에게 알립니다.\n4. 점검 결과를 일지에 기록합니다.',
           '/* 장비별 점검 기준은 제조사 지침과 내부 SOP를 우선합니다. */'
    UNION ALL
    SELECT '진료지원',
           '영상·검사 결과 전달 확인',
           '1. 긴급 결과 여부와 판독 상태를 확인합니다.\n2. 전달 대상 의료진 및 부서에 결과를 안내합니다.\n3. 전달 시각과 확인자를 기록합니다.\n4. 미확인 건은 재알림 절차를 진행합니다.',
           '/* 긴급 결과 전달은 병원의 공식 보고 체계를 따르세요. */'
) seed
JOIN MODUWMNL_CATEGORY root
  ON root.PARENT_CATEGORY_ID IS NULL AND root.CATEGORY_NM = '기본 업무' AND root.USE_YN = 'Y'
JOIN MODUWMNL_CATEGORY category
  ON category.PARENT_CATEGORY_ID = root.CATEGORY_ID
 AND category.CATEGORY_NM = seed.CATEGORY_NM
 AND category.USE_YN = 'Y'
WHERE NOT EXISTS (
    SELECT 1
    FROM MODUWMNL manual
    WHERE manual.CATEGORY_ID = category.CATEGORY_ID
      AND manual.MANUAL_TITLE = seed.MANUAL_TITLE
      AND manual.USE_YN = 'Y'
);

-- Attachments are shared by manuals and tasks.
CREATE TABLE IF NOT EXISTS MODU_ATTACH (
    ATTACH_ID        BIGINT NOT NULL AUTO_INCREMENT,
    OWNER_TP_CD      VARCHAR(20) NOT NULL,
    OWNER_ID         BIGINT NOT NULL,
    FILE_NM          VARCHAR(255) NOT NULL,
    FILE_PATH        VARCHAR(1000) NOT NULL,
    FILE_EXT         VARCHAR(20) NULL,
    USE_YN          CHAR(1) NOT NULL DEFAULT 'Y',
    FSR_DTM          DATETIME NOT NULL,
    FSR_STF_NO       VARCHAR(30) NULL,
    LSH_DTM          DATETIME NOT NULL,
    LSH_STF_NO       VARCHAR(30) NULL,
    PRIMARY KEY (ATTACH_ID),
    KEY IX_MODU_ATTACH_01 (OWNER_TP_CD, OWNER_ID, USE_YN)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
