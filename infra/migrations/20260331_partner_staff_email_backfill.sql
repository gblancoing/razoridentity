INSERT INTO core.partner_staff (id, tenant_id, partner_id, user_id, role, created_at)
SELECT (
           substr(md5('partner-staff:' || p.id::text || ':' || u.id::text), 1, 8) || '-' ||
           substr(md5('partner-staff:' || p.id::text || ':' || u.id::text), 9, 4) || '-' ||
           substr(md5('partner-staff:' || p.id::text || ':' || u.id::text), 13, 4) || '-' ||
           substr(md5('partner-staff:' || p.id::text || ':' || u.id::text), 17, 4) || '-' ||
           substr(md5('partner-staff:' || p.id::text || ':' || u.id::text), 21, 12)
       )::uuid,
       p.tenant_id,
       p.id,
       u.id,
       'owner',
       now()
FROM core.partners p
JOIN acl.users u
  ON lower(trim(coalesce(u.email, ''))) = lower(trim(coalesce(p.email, '')))
WHERE coalesce(trim(p.email), '') <> ''
  AND NOT EXISTS (
      SELECT 1
      FROM core.partner_staff ps
      WHERE ps.partner_id = p.id
  );
