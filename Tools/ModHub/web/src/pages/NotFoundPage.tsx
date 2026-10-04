import { Link } from 'react-router-dom';
import { Compass } from 'lucide-react';
import { Button } from '../components/ui/Button';
import { EmptyState } from '../components/ui/Feedback';

/** 404：给出唯一明确的下一步（回工坊首页），不放无意义的装饰 */
export default function NotFoundPage() {
  return (
    <div className="mx-auto max-w-3xl px-4 py-16">
      <EmptyState
        illustration
        title="页面不存在"
        description="链接可能已经失效，或者模组已被作者下架。"
        action={
          <Link to="/browse">
            <Button variant="gold" icon={<Compass aria-hidden className="h-4 w-4" />}>
              返回工坊首页
            </Button>
          </Link>
        }
      />
    </div>
  );
}
