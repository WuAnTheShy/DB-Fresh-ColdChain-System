-- 为已有 B 组 schema 补充优惠券叠加类型；全新 schema 已包含在 groupB_ddl.sql。
ALTER TABLE Mkt_Coupons ADD (
    CouponType VARCHAR2(20) DEFAULT 'NORMAL' NOT NULL
);

ALTER TABLE Mkt_Coupons ADD CONSTRAINT CK_Coupon_Type
    CHECK (CouponType IN ('NORMAL', 'SPECIAL'));
