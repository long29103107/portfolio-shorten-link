import { useEffect, useState } from "react";
import { ApiError } from "../api/http";
import { deactivateShortLink, getShortLinkDetails } from "../api/shortLinksApi";
import type { ShortLinkDetails } from "../types";
import { formatDateTime, toFriendlyErrorMessage } from "../types";
import { getExpiryPresentation } from "../expiryPresentation";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "../../../shared/components/ui/card";

type ShortLinkDetailPageProps = {
  code: string;
  onBackHome: () => void;
};

export function ShortLinkDetailPage({ code, onBackHome }: ShortLinkDetailPageProps) {
  const [details, setDetails] = useState<ShortLinkDetails | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isDeactivating, setIsDeactivating] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function loadDetails() {
      setIsLoading(true);
      setErrorMessage(null);

      try {
        const response = await getShortLinkDetails(code);
        if (!cancelled) {
          setDetails(response);
        }
      } catch (error) {
        if (cancelled) {
          return;
        }

        if (error instanceof ApiError) {
          setErrorMessage(toFriendlyErrorMessage(error.errorCode, error.message));
        } else {
          setErrorMessage("We could not load this short link right now.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void loadDetails();

    return () => {
      cancelled = true;
    };
  }, [code]);

  const handleDeactivate = async () => {
    setIsDeactivating(true);
    setErrorMessage(null);

    try {
      const response = await deactivateShortLink(code);
      setDetails((current) =>
        current
          ? {
              ...current,
              code: response.code,
              isActive: response.isActive
            }
          : current
      );
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setErrorMessage("The link could not be deactivated.");
      }
    } finally {
      setIsDeactivating(false);
    }
  };

  if (isLoading) {
    return (
      <Card className="panel-detail">
        <CardHeader>
          <p className="eyebrow">Details</p>
          <CardTitle>Loading {code}...</CardTitle>
        </CardHeader>
      </Card>
    );
  }

  if (!details) {
    return (
      <Card className="panel-detail">
        <CardHeader>
          <p className="eyebrow">Details</p>
          <CardTitle>{code}</CardTitle>
        </CardHeader>
        <CardContent>
        <p className="feedback feedback-error">{errorMessage ?? "This short link is missing."}</p>
        </CardContent>
        <CardFooter>
          <Button onClick={onBackHome}>
            Back home
          </Button>
        </CardFooter>
      </Card>
    );
  }

  return (
    <Card className="panel-detail">
      <CardHeader className="panel-heading-wide">
        <div>
          <p className="eyebrow">Details</p>
          <CardTitle>{details.code}</CardTitle>
        </div>
        <Badge variant={details.isActive ? "default" : "destructive"}>
          {details.isActive ? "Active" : "Deactivated"}
        </Badge>
      </CardHeader>

      <CardContent>
      <dl className="detail-list">
        <div>
          <dt>Destination</dt>
          <dd>
            <a href={details.originalUrl} target="_blank" rel="noreferrer">
              {details.originalUrl}
            </a>
          </dd>
        </div>
        <div>
          <dt>Created</dt>
          <dd>{formatDateTime(details.createdAtUtc)}</dd>
        </div>
        <div>
          <dt>Expiry</dt>
          <dd className="expiry-detail-value">
            {(() => {
              const expiry = getExpiryPresentation(details, new Date());
              return (
                <>
                  <time dateTime={details.expiredAtUtc ?? undefined}>{expiry.dateTime}</time>
                  <span className="expiry-detail-status">{expiry.detail}</span>
                </>
              );
            })()}
          </dd>
        </div>
      </dl>

      {errorMessage ? <p className="feedback feedback-error">{errorMessage}</p> : null}
      </CardContent>

      <CardFooter>
        <Button variant="secondary" onClick={onBackHome}>
          Back home
        </Button>
        <Button
          variant="destructive"
          onClick={handleDeactivate}
          disabled={!details.isActive || isDeactivating}
        >
          {isDeactivating ? "Deactivating..." : details.isActive ? "Deactivate link" : "Already inactive"}
        </Button>
      </CardFooter>
    </Card>
  );
}
