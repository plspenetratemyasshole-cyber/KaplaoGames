"use client";

import { Card, Typography, Alert } from "antd";
import { Suspense } from "react";
import { useSearchParams } from "next/navigation";

function SuccessBody() {
  const searchParams = useSearchParams();
  const device = searchParams.get("device");

  return (
    <Card style={{ width: 420, maxWidth: "92vw", textAlign: "center" }}>
      <Typography.Title level={2} style={{ marginTop: 0 }}>
        You are signed in
      </Typography.Title>
      {device ? (
        <>
          <Alert
            type="success"
            showIcon
            style={{ marginBottom: 16, textAlign: "left" }}
            message="Linked to the game"
            description={
              <>
                Return to the Kaplao / Unity window and click <strong>Sign in</strong> once more
                (this only refreshes — it will not open a new login). Your account should appear
                immediately.
              </>
            }
          />
          <Typography.Text type="secondary">Device code: {device}</Typography.Text>
        </>
      ) : (
        <Alert
          type="warning"
          showIcon
          style={{ textAlign: "left" }}
          message="Website only"
          description="This login was not started from the game. In Kaplao open Settings → Sign in, then log in in the tab that opens."
        />
      )}
    </Card>
  );
}

export default function SuccessPage() {
  return (
    <main style={{ minHeight: "100vh", display: "grid", placeItems: "center", padding: 24 }}>
      <Suspense fallback={<Card style={{ width: 420 }}>Loading…</Card>}>
        <SuccessBody />
      </Suspense>
    </main>
  );
}
