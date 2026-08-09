import { Button } from "@/shared/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/shared/components/ui/card";
import type { HttpStatusCode } from "../types";

type StatusPageProps = {
  statusCode: HttpStatusCode;
  onBackHome: () => void;
};

const statusCopy: Record<HttpStatusCode, { eyebrow: string; label: string; title: string; message: string }> = {
  401: {
    eyebrow: "401 Unauthorized",
    label: "Unauthorized",
    title: "Unauthorized",
    message: "This area needs a sign-in or access grant. Authentication is not wired into this demo yet."
  },
  403: {
    eyebrow: "403 Forbidden",
    label: "Forbidden",
    title: "Forbidden",
    message: "The route exists, but your current access would not be allowed to use it."
  },
  404: {
    eyebrow: "404 Not Found",
    label: "Not Found",
    title: "Not Found",
    message: "The page or short link could not be found. Try creating a new short link from the home screen."
  }
};

export function StatusPage({ statusCode, onBackHome }: StatusPageProps) {
  const copy = statusCopy[statusCode];

  return (
    <Card className="panel-detail status-page">
      <CardHeader>
        <p className="eyebrow">{copy.eyebrow}</p>
        <CardTitle>{copy.title}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="status-code-mark" aria-hidden="true">
          {statusCode} {copy.label}
        </div>
        <p className="muted-copy">{copy.message}</p>
      </CardContent>
      <CardFooter>
        <Button onClick={onBackHome}>
          Return to create flow
        </Button>
      </CardFooter>
    </Card>
  );
}
